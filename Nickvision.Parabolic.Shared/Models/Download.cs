using ATL;
using Nickvision.Desktop.Application;
using Nickvision.Desktop.Globalization;
using Nickvision.Desktop.Helpers;
using Nickvision.Parabolic.Shared.Events;
using Nickvision.Parabolic.Shared.Helpers;
using Nickvision.Parabolic.Shared.Services;
using System;
using System.Diagnostics;

using System.Threading;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace Nickvision.Parabolic.Shared.Models;

public partial class Download : IDisposable
{
    private const long NonProgressThrottleMs = 150;
    private static readonly string[] SourceDataAdditionalFieldKeys =
    [
        "comment",
        "COMMENT",
        "description",
        "DESCRIPTION",
        "purl",
        "PURL",
        "synopsis",
        "SYNOPSIS",
        "url",
        "URL"
    ];

    private static int _nextId;

    private readonly IConfigurationService _configurationService;
    private readonly ITranslationService _translationService;
    private readonly IYtdlpExecutableService _ytdlpExecutableService;
    private readonly BoundedLog _logLines;
    private string? _finalPath;
    private readonly object _processLock = new();
    private bool _removeSourceData;
    private Process? _process;
    private int _progressSkipCounter;
    private long _lastNonProgressTicks;

    public int Id { get; }
    public DownloadOptions Options { get; }
    public string FilePath { get; private set; }
    public DownloadStatus Status { get; private set; }
    public string Log => _logLines.ToString();

    public event EventHandler<DownloadCompletedEventArgs>? Completed;
    public event EventHandler<DownloadProgressChangedEventArgs>? ProgressChanged;

    static Download()
    {
        _nextId = 0;
    }

    public Download(IConfigurationService configurationService, ITranslationService translationService, IYtdlpExecutableService ytdlpExecutableService, DownloadOptions options)
    {
        _configurationService = configurationService;
        _translationService = translationService;
        _ytdlpExecutableService = ytdlpExecutableService;
        _logLines = new BoundedLog();
        _removeSourceData = false;
        _process = null;
        _progressSkipCounter = 0;
        _lastNonProgressTicks = Environment.TickCount64 - NonProgressThrottleMs;
        Id = Interlocked.Increment(ref _nextId);
        Options = options;
        FilePath = Path.Combine(Options.SaveFolder, $"{Options.SaveFilename}{Options.FileType.DotExtension}");
        Status = DownloadStatus.Queued;
    }

    ~Download()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void Pause()
    {
        lock (_processLock)
        {
            if (Status != DownloadStatus.Running)
            {
                return;
            }
            Status = DownloadStatus.Paused;
            _process?.Suspend();

        }
    }

    public void Resume()
    {
        lock (_processLock)
        {
            if (Status != DownloadStatus.Paused)
            {
                return;
            }
            Status = DownloadStatus.Running;
            _process?.Resume();
            ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, ReadOnlyMemory<char>.Empty, double.NaN, 0.0, 0));

        }
    }

    public void Start()
    {
        lock (_processLock)
        {
            if (Status == DownloadStatus.Running || Status == DownloadStatus.Paused)
            {
                return;
            }
            if (File.Exists(FilePath) && !_configurationService.OverwriteExistingFiles)
            {
                var log = _translationService?._("The file already exists and overwriting is disabled.") ?? "The file already exists and overwriting is disabled.";
                _logLines.Enqueue(log);
                Status = DownloadStatus.Error;
                ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, log.AsMemory(), double.NaN, 0.0, 0));
                Completed?.Invoke(this, new DownloadCompletedEventArgs(Id, Status, FilePath, log.AsMemory(), false));
                return;
            }
            try
            {
                _removeSourceData = _configurationService.RemoveSourceData;
                _process = _ytdlpExecutableService.GetDownloadProcess(Options);
                Status = DownloadStatus.Running;

                _process.OutputDataReceived += Process_StandardOutputReceived;
                _process.ErrorDataReceived += Process_OutputDataReceived;
                _process.Start();
                _process.SetAsParentProcess();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
                ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, (_translationService?._("Starting download...") ?? "Starting download...").AsMemory(), double.NaN, 0.0, 0));
                _ = CompleteProcessAsync(_process);
            }
            catch (Exception error)
            {
                _logLines.Enqueue(error.ToString());
                Status = DownloadStatus.Error;
                if (_process is not null)
                {
                    try
                    {
                        if (!_process.HasExited) { _process.Kill(true); }
                    }
                    catch (InvalidOperationException) { }
                    _process.Dispose();
                    _process = null;
                }
                Completed?.Invoke(this, new DownloadCompletedEventArgs(Id, Status, FilePath, Log.AsMemory(), true));
            }

        }
    }

    public void Stop()
    {
        lock (_processLock)
        {
            if (Status is DownloadStatus.Success or DownloadStatus.Error or DownloadStatus.Stopped)
            {
                return;
            }
            Status = DownloadStatus.Stopped;
            if (_process is { HasExited: false } process)
            {
                try
                {
                    process.Kill(true);
                }
                catch (InvalidOperationException) when (process.HasExited)
                {
                    // The process may exit between the check and Kill.
                }
            }

        }
    }

    private void Dispose(bool disposing)
    {
        lock (_processLock)
        {
            if (!disposing)
            {
                return;
            }
            if (_process is not null && !_process.HasExited)
            {
                Stop();
            }

        }
    }

    private async Task CompleteProcessAsync(Process process)
    {
        try
        {
            await process.WaitForExitAsync().ConfigureAwait(false);
            lock (_processLock)
            {
                if (Status != DownloadStatus.Stopped)
                {
                    Status = process.ExitCode == 0 ? DownloadStatus.Success : DownloadStatus.Error;
                }
            }
            if (Status == DownloadStatus.Success)
            {
                try
                {
                    var finalPath = _finalPath;
                    if (!string.IsNullOrEmpty(finalPath) && File.Exists(finalPath))
                    {
                        FilePath = finalPath;
                        if (_removeSourceData)
                        {
                            var track = new Track(FilePath);
                            var hasSourceData = !string.IsNullOrEmpty(track.Comment)
                                || !string.IsNullOrEmpty(track.Description)
                                || !string.IsNullOrEmpty(track.EncodedBy)
                                || !string.IsNullOrEmpty(track.Encoder);
                            if (!string.IsNullOrEmpty(track.Comment))
                            {
                                track.Comment = string.Empty;
                            }
                            if (!string.IsNullOrEmpty(track.Description))
                            {
                                track.Description = string.Empty;
                            }
                            if (!string.IsNullOrEmpty(track.EncodedBy))
                            {
                                track.EncodedBy = string.Empty;
                            }
                            if (!string.IsNullOrEmpty(track.Encoder))
                            {
                                track.Encoder = string.Empty;
                            }
                            foreach (var key in SourceDataAdditionalFieldKeys)
                            {
                                if (track.AdditionalFields.Remove(key))
                                {
                                    hasSourceData = true;
                                }
                            }
                            if (hasSourceData)
                            {
                                await track.SaveAsync();
                            }
                        }
                    }
                }
                catch { }
            }
        }
        catch (Exception error)
        {
            _logLines.Enqueue(error.ToString());
            if (Status != DownloadStatus.Stopped)
            {
                Status = DownloadStatus.Error;
            }
        }
        finally
        {
            lock (_processLock)
            {
                process.ErrorDataReceived -= Process_OutputDataReceived;
                process.OutputDataReceived -= Process_StandardOutputReceived;
                process.Dispose();
                _process = null;
            }
        }
        ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, ReadOnlyMemory<char>.Empty));
        Completed?.Invoke(this, new DownloadCompletedEventArgs(Id, Status, FilePath, Log.AsMemory(), true));
    }
    public static void ReserveIdsThrough(int id)
    {
        int current;
        do
        {
            current = Volatile.Read(ref _nextId);
            if (current >= id)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref _nextId, id, current) != current);
    }

    private void Process_StandardOutputReceived(object? sender, DataReceivedEventArgs e)
    {
        const string prefix = "__BEARDL_FILE__";
        if (e.Data?.StartsWith(prefix, StringComparison.Ordinal) == true)
        {
            _finalPath = e.Data[prefix.Length..];
            _logLines.Enqueue(_finalPath);
            return;
        }
        Process_OutputDataReceived(sender, e);
    }
    private async void Process_OutputDataReceived(object? sender, DataReceivedEventArgs e)
    {
        if (_progressSkipCounter > 0 || e.Data is null || string.IsNullOrEmpty(e.Data) || string.IsNullOrWhiteSpace(e.Data) || e.Data.StartsWith(" ***", StringComparison.Ordinal))
        {
            if (_progressSkipCounter > 0)
            {
                _progressSkipCounter--;
            }
            else if (e.Data?.StartsWith(" ***", StringComparison.Ordinal) ?? false)
            {
                _progressSkipCounter = 4;
            }
            return;
        }
        _logLines.Enqueue(e.Data);
        try
        {
            if (e.Data.StartsWith("[Parabolic] Progress", StringComparison.Ordinal))
            {
                if (!TryParseParabolicProgress(e.Data, out var progress, out var speed, out var eta))
                {
                    progress = double.NaN;
                    speed = 0.0;
                    eta = 0;
                }
                ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, e.Data.AsMemory(), progress, speed, eta));
            }
            else if (e.Data.StartsWith("[#", StringComparison.Ordinal))
            {
                var line = e.Data;
                if (OperatingSystem.IsWindows())
                {
                    var index = line.LastIndexOf('\r');
                    while (index >= 0)
                    {
                        var candidate = line[(index + 1)..];
                        if (candidate.TryParseAriaProgressLine(out var progress, out var speed, out var eta))
                        {
                            ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, e.Data.AsMemory(), progress, speed, eta));
                            return;
                        }
                        line = line[..index];
                        index = line.LastIndexOf('\r');
                    }
                }
                if (!line.TryParseAriaProgressLine(out var finalProgress, out var finalSpeed, out var finalEta))
                {
                    EmitThrottledNonProgressUpdate(e.Data);
                    return;
                }
                ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, e.Data.AsMemory(), finalProgress, finalSpeed, finalEta));
            }
            else if (e.Data.StartsWith("[download] Sleeping", StringComparison.Ordinal))
            {
                var seconds = 0.0;
                var sleepingPrefixLength = "[download] Sleeping ".Length;
                var secondsEnd = e.Data.IndexOf(" second", sleepingPrefixLength, StringComparison.Ordinal);
                if (secondsEnd == -1 || !double.TryParse(e.Data.AsSpan(sleepingPrefixLength, secondsEnd - sleepingPrefixLength), NumberStyles.Any, CultureInfo.InvariantCulture, out seconds))
                {
                    ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, e.Data.AsMemory(), double.NegativeInfinity, 0.0, 0));
                }
                else
                {
                    ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, e.Data.AsMemory(), double.NegativeInfinity, seconds, 0));
                    while (seconds >= 1)
                    {
                        if (Status != DownloadStatus.Running)
                        {
                            return;
                        }
                        ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, ReadOnlyMemory<char>.Empty, double.NegativeInfinity, Math.Floor(seconds--), 0));
                        await Task.Delay(1000);
                    }
                    ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, ReadOnlyMemory<char>.Empty, double.NaN, 0.0, 0));
                }
            }
            else
            {
                EmitThrottledNonProgressUpdate(e.Data);
            }
        }
        catch
        {
            EmitThrottledNonProgressUpdate(e.Data);
        }
    }

    private void EmitThrottledNonProgressUpdate(string line)
    {
        var now = Environment.TickCount64;
        var last = Volatile.Read(ref _lastNonProgressTicks);
        if (now - last < NonProgressThrottleMs || Interlocked.CompareExchange(ref _lastNonProgressTicks, now, last) != last)
        {
            return;
        }
        ProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(Id, line.AsMemory(), double.NaN, 0.0, 0));
    }

    private static bool TryParseParabolicProgress(ReadOnlySpan<char> line, out double progress, out double speed, out int eta)
    {
        progress = double.NaN;
        speed = 0.0;
        eta = 0;
        if (!TryNextField(ref line, out _)
            || !TryNextField(ref line, out var status)
            || !TryNextField(ref line, out var downloadedSpan)
            || !TryNextField(ref line, out var totalSpan)
            || !TryNextField(ref line, out var totalEstimateSpan)
            || !TryNextField(ref line, out var speedSpan)
            || line.Contains(';'))
        {
            return false;
        }
        var etaSpan = line;
        if (status.Equals("NA", StringComparison.Ordinal)
            || status.Equals("finished", StringComparison.Ordinal)
            || status.Equals("processing", StringComparison.Ordinal))
        {
            return true;
        }
        var downloaded = 0.0;
        if (!downloadedSpan.Equals("NA", StringComparison.Ordinal)
            && !double.TryParse(downloadedSpan, NumberStyles.Any, CultureInfo.InvariantCulture, out downloaded))
        {
            return false;
        }
        double total;
        if (!totalSpan.Equals("NA", StringComparison.Ordinal))
        {
            if (!double.TryParse(totalSpan, NumberStyles.Any, CultureInfo.InvariantCulture, out total))
            {
                return false;
            }
        }
        else if (!totalEstimateSpan.Equals("NA", StringComparison.Ordinal))
        {
            if (!double.TryParse(totalEstimateSpan, NumberStyles.Any, CultureInfo.InvariantCulture, out total))
            {
                return false;
            }
        }
        else
        {
            total = 1.0;
        }
        if (!speedSpan.Equals("NA", StringComparison.Ordinal)
            && !double.TryParse(speedSpan, NumberStyles.Any, CultureInfo.InvariantCulture, out speed))
        {
            return false;
        }
        if (etaSpan.Equals("NA", StringComparison.Ordinal) || etaSpan.Equals("Unknown", StringComparison.Ordinal))
        {
            eta = -1;
        }
        else if (double.TryParse(etaSpan, NumberStyles.Any, CultureInfo.InvariantCulture, out var etaSeconds))
        {
            eta = Convert.ToInt32(etaSeconds);
        }
        else
        {
            return false;
        }
        progress = downloaded / total;
        return true;
    }

    private static bool TryNextField(ref ReadOnlySpan<char> remaining, out ReadOnlySpan<char> field)
    {
        var separatorIndex = remaining.IndexOf(';');
        if (separatorIndex < 0)
        {
            field = default;
            return false;
        }
        field = remaining[..separatorIndex];
        remaining = remaining[(separatorIndex + 1)..];
        return true;
    }
}
