using Microsoft.Extensions.Logging;
using Nickvision.Desktop.Application;
using Nickvision.Desktop.Globalization;
using Nickvision.Parabolic.Shared.Events;
using Nickvision.Parabolic.Shared.Helpers;
using Nickvision.Parabolic.Shared.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nickvision.Parabolic.Shared.Services;

public class DownloadService : IDisposable, IDownloadService
{
    private readonly ILogger<DownloadService> _logger;
    private readonly IConfigurationService _configurationService;
    private readonly IHistoryService _historyService;
    private readonly IRecoveryService _recoveryService;
    private readonly ITranslationService _translationService;
    private readonly IYtdlpExecutableService _ytdlpService;
    private readonly ConcurrentDictionary<int, Download> _downloading = new();
    private readonly ConcurrentDictionary<int, Download> _queued = new();
    private readonly ConcurrentDictionary<int, Download> _completed = new();
    private readonly Queue<int> _queueOrder = new();
    private readonly SemaphoreSlim _additionLock = new(1, 1);
    private readonly SemaphoreSlim _mutationLock = new(1, 1);
    private volatile bool _disposed;
    private bool _idsInitialized;

    public event EventHandler<DownloadAddedEventArgs>? DownloadAdded;
    public event EventHandler<DownloadCompletedEventArgs>? DownloadCompleted;
    public event EventHandler<DownloadProgressChangedEventArgs>? DownloadProgressChanged;
    public event EventHandler<DownloadEventArgs>? DownloadRetired;
    public event EventHandler<DownloadEventArgs>? DownloadStartedFromQueue;
    public event EventHandler<DownloadEventArgs>? DownloadStopped;
    public int DownloadingCount => _downloading.Count;
    public int QueuedCount => _queued.Count;
    public int CompletedCount => _completed.Count;
    public int FailedCount => _completed.Values.Count(download => download.Status == DownloadStatus.Error);

    public DownloadService(ILogger<DownloadService> logger, IConfigurationService configurationService, IHistoryService historyService, IRecoveryService recoveryService, ITranslationService translationService, IYtdlpExecutableService ytdlpService)
    {
        _logger = logger;
        _configurationService = configurationService;
        _historyService = historyService;
        _recoveryService = recoveryService;
        _translationService = translationService;
        _ytdlpService = ytdlpService;
    }

    public async Task<bool> AddWithDuplicateCheckAsync(IReadOnlyList<DownloadOptions> options, bool excludeFromHistory, Func<IReadOnlyList<DuplicateDownload>, Task<bool>> confirm)
    {
        // Serialize submissions, never hold the state gate while waiting for UI.
        await _additionLock.WaitAsync();
        try
        {
            if (_configurationService.WarnBeforeDuplicateDownload)
            {
                var urls = options.Select(item => item.Url).ToHashSet();
                var saved = new Dictionary<Uri, string>(await _historyService.GetSavedFilesAsync(urls));
                HashSet<Uri> active;
                await _mutationLock.WaitAsync();
                try
                {
                    foreach (var item in _completed.Values)
                    {
                        if (urls.Contains(item.Options.Url) && item.Status == DownloadStatus.Success)
                        {
                            saved[item.Options.Url] = item.FilePath;
                        }
                    }
                    active = _downloading.Values.Concat(_queued.Values).Select(item => item.Options.Url).Where(urls.Contains).ToHashSet();
                }
                finally
                {
                    _mutationLock.Release();
                }
                var duplicates = DuplicateDownloadDetector.Find(options.Select(item => item.Url), active, saved);
                if (duplicates.Count > 0 && !await confirm(duplicates))
                {
                    return false;
                }
            }
            await AddAsync(options, excludeFromHistory);
            return true;
        }
        finally
        {
            _additionLock.Release();
        }
    }

    public Task AddAsync(DownloadOptions options, bool excludeFromHistory) => AddAsync(new[] { options }, excludeFromHistory);

    public async Task AddAsync(IReadOnlyList<DownloadOptions> options, bool excludeFromHistory)
    {
        await _mutationLock.WaitAsync();
        try
        {
            await AddCoreAsync(options, excludeFromHistory);
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    // Caller owns the mutation gate. Commit recovery before publishing rows.
    private async Task AddCoreAsync(IReadOnlyList<DownloadOptions> options, bool excludeFromHistory, IReadOnlyList<int>? recoveredIds = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_idsInitialized)
        {
            var existing = await _recoveryService.GetAllAsync();
            if (existing.Count > 0)
            {
                Download.ReserveIdsThrough(existing.Max(record => record.Id));
            }
            _idsInitialized = true;
        }
        var downloads = new List<Download>(options.Count);
        foreach (var option in options)
        {
            if (recoveredIds is null)
            {
                option.ExcludeFromHistory = excludeFromHistory;
            }
            downloads.Add(new Download(_configurationService, _translationService, _ytdlpService, option));
        }
        var records = downloads.Select(download => new RecoverableDownload(download.Id, download.Options)).ToList();
        var persisted = recoveredIds is null
            ? await _recoveryService.AddAsync(records)
            : await _recoveryService.ReplaceAsync(recoveredIds, records);
        if (!persisted)
        {
            throw new IOException("Unable to save download recovery data.");
        }
        ObjectDisposedException.ThrowIf(_disposed, this);
        var ready = new List<Download>();
        foreach (var download in downloads)
        {
            download.Completed += Download_Completed;
            download.ProgressChanged += Download_ProgressChanged;
            if (_queued.IsEmpty && _downloading.Count < _configurationService.MaxNumberOfActiveDownloads)
            {
                _downloading[download.Id] = download;
                ready.Add(download);
                DownloadAdded?.Invoke(this, new DownloadAddedEventArgs(download.Id, download.FilePath, download.Options.Url, DownloadStatus.Running));
            }
            else
            {
                _queued[download.Id] = download;
                _queueOrder.Enqueue(download.Id);
                DownloadAdded?.Invoke(this, new DownloadAddedEventArgs(download));
            }
        }
        foreach (var download in ready)
        {
            if (!_disposed)
            {
                download.Start();
            }
        }
        StartQueued();
    }

    private void StartQueued()
    {
        while (!_disposed && _downloading.Count < _configurationService.MaxNumberOfActiveDownloads && _queueOrder.TryDequeue(out var id))
        {
            if (_queued.TryRemove(id, out var download))
            {
                _downloading[id] = download;
                DownloadStartedFromQueue?.Invoke(this, new DownloadEventArgs(id));
                download.Start();
            }
        }
    }

    public IReadOnlyList<int> ClearCompleted()
    {
        var ids = new List<int>();
        foreach (var id in _completed.Keys)
        {
            if (_completed.TryRemove(id, out var download))
            {
                Detach(download);
                download.Dispose();
                ids.Add(id);
            }
        }
        return ids;
    }

    public async Task<IReadOnlyList<int>> ClearQueuedAsync()
    {
        await _mutationLock.WaitAsync();
        try
        {
            var ids = _queued.Keys.ToList();
            if (!await _recoveryService.RemoveAsync(ids))
            {
                throw new IOException("Unable to remove queued recovery data.");
            }
            foreach (var id in ids)
            {
                if (_queued.TryRemove(id, out var download))
                {
                    Detach(download);
                    download.Dispose();
                }
            }
            _queueOrder.Clear();
            return ids;
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    public bool Pause(int id)
    {
        if (!_downloading.TryGetValue(id, out var download))
        {
            return false;
        }
        download.Pause();
        return true;
    }

    public bool Resume(int id)
    {
        if (!_downloading.TryGetValue(id, out var download))
        {
            return false;
        }
        download.Resume();
        return true;
    }

    public async Task RecoverAllAsync()
    {
        await _mutationLock.WaitAsync();
        try
        {
            var records = await _recoveryService.GetAllAsync();
            var pending = records.Where(record => !_downloading.ContainsKey(record.Id) && !_queued.ContainsKey(record.Id)).ToList();
            if (pending.Count == 0)
            {
                return;
            }
            Download.ReserveIdsThrough(records.Max(record => record.Id));
            await AddCoreAsync(pending.Select(record => record.Options).ToList(), false, pending.Select(record => record.Id).ToList());
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    public async Task<bool> RetryAsync(int id)
    {
        await _mutationLock.WaitAsync();
        try
        {
            if (!_completed.TryGetValue(id, out var download))
            {
                return false;
            }
            await AddCoreAsync(new[] { download.Options }, download.Options.ExcludeFromHistory);
            _completed.TryRemove(id, out _);
            Detach(download);
            download.Dispose();
            DownloadRetired?.Invoke(this, new DownloadEventArgs(id));
            return true;
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    public async Task RetryFailedAsync()
    {
        foreach (var id in _completed.Values.Where(download => download.Status == DownloadStatus.Error).Select(download => download.Id).ToArray())
        {
            await RetryAsync(id);
        }
    }

    public async Task<bool> StopAsync(int id)
    {
        await _mutationLock.WaitAsync();
        try
        {
            if (!_downloading.TryGetValue(id, out var download) && !_queued.TryGetValue(id, out download))
            {
                return false;
            }
            try
            {
                await StopCoreAsync(new[] { download });
            }
            finally
            {
                StartQueued();
            }
            return true;
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    public async Task StopAllAsync()
    {
        await _mutationLock.WaitAsync();
        try
        {
            await StopCoreAsync(_downloading.Values.Concat(_queued.Values).ToArray());
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    private async Task StopCoreAsync(IReadOnlyList<Download> downloads)
    {
        foreach (var download in downloads)
        {
            download.Stop();
            Detach(download);
            _downloading.TryRemove(download.Id, out _);
            _queued.TryRemove(download.Id, out _);
            _completed[download.Id] = download;
            DownloadStopped?.Invoke(this, new DownloadEventArgs(download.Id));
        }
        if (!await _recoveryService.RemoveAsync(downloads.Select(download => download.Id).ToList()))
        {
            throw new IOException("Unable to remove stopped recovery data.");
        }
        if (_queued.IsEmpty)
        {
            _queueOrder.Clear();
        }
    }

    private async void Download_Completed(object? sender, DownloadCompletedEventArgs e)
    {
        await _mutationLock.WaitAsync();
        try
        {
            if (!_downloading.TryRemove(e.Id, out var download))
            {
                return;
            }
            _completed[e.Id] = download;
            try
            {
                if (e.Status == DownloadStatus.Success && !download.Options.ExcludeFromHistory)
                {
                    if (!await _historyService.AddAsync(new HistoricDownload(download.Options.Url)
                    {
                        Title = Path.GetFileNameWithoutExtension(download.FilePath),
                        Path = download.FilePath
                    }))
                    {
                        throw new IOException("Unable to save completed download history.");
                    }
                }
                if (!await _recoveryService.RemoveAsync(e.Id))
                {
                    throw new IOException("Unable to remove completed recovery data.");
                }
            }
            catch (Exception error)
            {
                _logger.LogError(error, "Unable to persist completed download {Id}", e.Id);
            }
            DownloadCompleted?.Invoke(this, e);
            StartQueued();
        }
        catch (Exception error)
        {
            _logger.LogError(error, "Unable to complete download {Id}", e.Id);
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    private void Download_ProgressChanged(object? sender, DownloadProgressChangedEventArgs e)
    {
        if (_downloading.TryGetValue(e.Id, out var download) && download.Status == DownloadStatus.Running)
        {
            DownloadProgressChanged?.Invoke(this, e);
        }
    }

    private void Detach(Download download)
    {
        download.Completed -= Download_Completed;
        download.ProgressChanged -= Download_ProgressChanged;
    }

    public void Dispose()
    {
        _disposed = true;
        foreach (var download in _downloading.Values.Concat(_queued.Values).Concat(_completed.Values))
        {
            Detach(download);
            download.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
