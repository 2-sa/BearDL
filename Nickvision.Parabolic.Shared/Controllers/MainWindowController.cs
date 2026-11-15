using Microsoft.Extensions.Logging;
using Nickvision.Desktop.Application;
using Nickvision.Desktop.Filesystem;
using Nickvision.Desktop.Globalization;
using Nickvision.Desktop.Network;
using Nickvision.Desktop.Notifications;
using Nickvision.Desktop.System;
using Nickvision.Parabolic.Shared.Helpers;
using Nickvision.Parabolic.Shared.Models;
using Nickvision.Parabolic.Shared.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Nickvision.Parabolic.Shared.Controllers;

public class MainWindowController
{
    private readonly ILogger<MainWindowController> _logger;
    private readonly AppInfo _appInfo;
    private readonly IArgumentsService _argumentsService;
    private readonly IConfigurationService _configurationService;
    private readonly IDatabaseService _databaseService;
    private readonly IDenoExecutableService _denoExecutableService;
    private readonly IDownloadService _downloadService;
    private readonly INotificationService _notificationService;
    private readonly IPowerService _powerService;
    private readonly IRecoveryService _recoveryService;
    private readonly ITranslationService _translationService;
    private readonly IUpdaterService _updaterService;
    private readonly IYtdlpExecutableService _ytdlpExecutableService;
    private AppVersion _latestAppVersion;
    private AppVersion _latestYtdlpVersion;
    private AppVersion _latestDenoVersion;

    public MainWindowController(ILogger<MainWindowController> logger, AppInfo appInfo, IArgumentsService argumentsService, IConfigurationService configurationService, IDatabaseService databaseService, IDenoExecutableService denoExecutableService, IDownloadService downloadService, INotificationService notificationService, IPowerService powerService, IRecoveryService recoveryService, ITranslationService translationService, IUpdaterService updaterService, IYtdlpExecutableService ytdlpExecutableService)
    {
        _logger = logger;
        _appInfo = appInfo;
        _argumentsService = argumentsService;
        _configurationService = configurationService;
        _databaseService = databaseService;
        _denoExecutableService = denoExecutableService;
        _downloadService = downloadService;
        _notificationService = notificationService;
        _powerService = powerService;
        _recoveryService = recoveryService;
        _translationService = translationService;
        _updaterService = updaterService;
        _ytdlpExecutableService = ytdlpExecutableService;
        _latestAppVersion = appInfo.Version!;
        _latestYtdlpVersion = _ytdlpExecutableService.BundledVersion;
        _latestDenoVersion = _denoExecutableService.BundledVersion;
        _translationService.Language = _configurationService.TranslationLanguage;
        _logger.LogInformation($"Received command-line arguments: [{string.Join(", ", argumentsService.Data)}]");
        // Events
        _configurationService.Saved += ConfigurationService_Saved;
        // Translate strings
        _appInfo.ShortName = "BearDL";
        _appInfo.Description = translationService._("Download web video and audio.");
        _appInfo.ExtraLinks.Add(translationService._("Matrix Chat"), new Uri("https://matrix.to/#/#nickvision:matrix.org"));
        _appInfo.Developers.Add("Nicholas Logozzo", "https://github.com/nlogozzo");
        _appInfo.Developers.Add(translationService._("Contributors on GitHub ❤️"), "https://github.com/NickvisionApps/Parabolic/graphs/contributors");
        _appInfo.Designers.Add("Nicholas Logozzo", "https://github.com/nlogozzo");
        _appInfo.Designers.Add(translationService._("Fyodor Sobolev"), "https://github.com/fsobolev");
        _appInfo.Designers.Add("DaPigGuy", "https://github.com/DaPigGuy");
        _appInfo.Artists.Add(translationService._("David Lapshin"), "https://github.com/daudix");
        _appInfo.TranslationCredits = translationService._("translation-credits");
    }

    public bool CanShutdown => _downloadService.RemainingCount == 0;

    public int CompletedDownloadsCount => _downloadService.CompletedCount;

    public int FailedDownloadsCount => _downloadService.FailedCount;

    public int QueuedDownloadsCount => _downloadService.QueuedCount;

    public int RecoverableDownloadsCount => _recoveryService.Count;

    public int RemainingDownloadsCount => _downloadService.RemainingCount;

    public int RunningDownloadsCount => _downloadService.DownloadingCount;

    public string SaveFolder
    {
        get => _configurationService.PreviousSaveFolder;

        set => _configurationService.PreviousSaveFolder = value;
    }

    public bool ShowDisclaimerOnStartup
    {
        get => _configurationService.ShowDisclaimerOnStartup;

        set => _configurationService.ShowDisclaimerOnStartup = value;
    }

    public Theme Theme => _configurationService.Theme;

    public WindowGeometry WindowGeometry
    {
        get => _configurationService.WindowGeometry;

        set => _configurationService.WindowGeometry = value;
    }

    public Uri? UrlFromArgs
    {
        get
        {
            for (var i = 0; i < _argumentsService.Data.Count; i++)
            {
                var urlText = _argumentsService.Data[i].Trim();
                if (LaunchUrlParser.Parse(urlText) is Uri launchUrl)
                {
                    return launchUrl;
                }
                if (Uri.TryCreate(urlText, UriKind.Absolute, out var url) && url.IsFile)
                {
                    return url;
                }
            }
            return null;
        }
    }

    public async Task CheckForUpdatesAsync(bool showNotificationForNoUpdates)
    {
        _logger.LogInformation("Checking for updates...");
        if (showNotificationForNoUpdates)
        {
            // The shared updater service otherwise reuses GitHub's release list for up to six hours.
            RefreshAppReleaseCache();
        }
        var stableAppVersionTask = _updaterService.GetLatestStableVersionAsync();
        var stableYtdlpVersionTask = _ytdlpExecutableService.GetLatestStableVersionAsync();
        var stableDenoVersionTask = _denoExecutableService.GetLatestStableVersionAsync();
        await Task.WhenAll(stableAppVersionTask, stableYtdlpVersionTask, stableDenoVersionTask);
        var stableAppVersion = await stableAppVersionTask;
        var stableYtdlpVersion = await stableYtdlpVersionTask;
        var stableDenoVersion = await stableDenoVersionTask;
        if (stableAppVersion is not null)
        {
            _latestAppVersion = stableAppVersion;
        }
        if (stableYtdlpVersion is not null)
        {
            _latestYtdlpVersion = stableYtdlpVersion;
        }
        if (stableDenoVersion is not null)
        {
            _latestDenoVersion = stableDenoVersion;
        }
        if (_configurationService.AllowPreviewUpdates)
        {
            var previewAppVersionTask = _updaterService.GetLatestPreviewVersionAsync();
            var previewYtdlpVersionTask = _ytdlpExecutableService.GetLatestPreviewVersionAsync();
            await Task.WhenAll(previewAppVersionTask, previewYtdlpVersionTask);
            var previewAppVersion = await previewAppVersionTask;
            var previewYtdlpVersion = await previewYtdlpVersionTask;
            if (previewAppVersion is not null && previewAppVersion > stableAppVersion)
            {
                _latestAppVersion = previewAppVersion;
            }
            if (previewYtdlpVersion is not null && previewYtdlpVersion > stableYtdlpVersion)
            {
                _latestYtdlpVersion = previewYtdlpVersion;
            }
        }
        if (_latestAppVersion > _appInfo.Version!)
        {
            if (!OperatingSystem.IsLinux())
            {
                _logger.LogInformation($"New application update available: {_latestAppVersion}");
                _notificationService.Send(new AppNotification(_translationService._("New {0} update available: {1}", _appInfo.ShortName!, _latestAppVersion.ToString()), NotificationSeverity.Success)
                {
                    Action = "update"
                });
            }
        }
        else if (_latestYtdlpVersion > _ytdlpExecutableService.BundledVersion && _latestYtdlpVersion > _ytdlpExecutableService.InstalledVersion)
        {
            _logger.LogInformation($"New yt-dlp update available: {_latestYtdlpVersion}");
            _notificationService.Send(new AppNotification(_translationService._("New yt-dlp update available: {0}", _latestYtdlpVersion.ToString()), NotificationSeverity.Success)
            {
                Action = "update-ytdlp"
            });
        }
        else if (_latestDenoVersion > _denoExecutableService.BundledVersion && _latestDenoVersion > _denoExecutableService.InstalledVersion)
        {
            _logger.LogInformation($"New deno update available: {_latestDenoVersion}");
            _notificationService.Send(new AppNotification(_translationService._("New deno update available: {0}", _latestDenoVersion.ToString()), NotificationSeverity.Success)
            {
                Action = "update-deno"
            });
        }
        else
        {
            _logger.LogInformation("No application updates available.");
            if (showNotificationForNoUpdates)
            {
                _notificationService.Send(new AppNotification(_translationService._("No update available"), NotificationSeverity.Warning));
            }
        }
    }

    private void RefreshAppReleaseCache()
    {
        if (_appInfo.SourceRepository is null)
        {
            return;
        }
        var repositoryParts = _appInfo.SourceRepository.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (repositoryParts.Length < 2)
        {
            return;
        }
        var cachePath = Path.Combine(UserDirectories.Cache, "Nickvision", $"{repositoryParts[0]}-{repositoryParts[1]}-releases.json");
        try
        {
            if (File.Exists(cachePath))
            {
                File.Delete(cachePath);
                _logger.LogInformation("Cleared cached GitHub releases before a manual update check.");
            }
        }
        catch (Exception e)
        {
            _logger.LogWarning($"Unable to clear cached GitHub releases before a manual update check: {e}");
        }
    }

    public IEnumerable<int> ClearCompletedDownloads()
    {
        try
        {
            return _downloadService.ClearCompleted();
        }
        catch (Exception e)
        {
            _logger.LogError($"An error occurred while clearing completed downloads: {e}");
            _notificationService.Send(new AppNotification(_translationService._("An error occurred while clearing completed downloads"), NotificationSeverity.Error)
            {
                Action = "error",
                ActionParam = e.ToString()
            });
            return [];
        }
    }

    public async Task ClearRecoverableDownloadsAsync()
    {
        try
        {
            await _recoveryService.ClearAsync();
        }
        catch (Exception e)
        {
            _logger.LogError($"An error occurred while clearing recoverable downloads: {e}");
            _notificationService.Send(new AppNotification(_translationService._("An error occurred while clearing recoverable downloads"), NotificationSeverity.Error)
            {
                Action = "error",
                ActionParam = e.ToString()
            });
        }
    }

    public async Task<IEnumerable<int>> ClearQueuedDownloadsAsync()
    {
        try
        {
            return await _downloadService.ClearQueuedAsync();
        }
        catch (Exception e)
        {
            _logger.LogError($"An error occurred while clearing queued downloads: {e}");
            _notificationService.Send(new AppNotification(_translationService._("An error occurred while clearing queued downloads"), NotificationSeverity.Error)
            {
                Action = "error",
                ActionParam = e.ToString()
            });
            return [];
        }
    }

    public async Task DenoUpdateAsync(IProgress<DownloadProgress> progress)
    {
        var res = await _denoExecutableService.DownloadUpdateAsync(_latestDenoVersion, progress);
        if (res)
        {
            _notificationService.Send(new AppNotification(_translationService._("Deno {0} installed successfully", _latestDenoVersion.ToString()), NotificationSeverity.Success));
        }
        else
        {
            _notificationService.Send(new AppNotification(_translationService._("Unable to download and install the Deno update"), NotificationSeverity.Error));
        }
    }

    public async Task<string> GetDebugInformationAsync(string extraInformation = "")
    {
        var ytdlpVersion = await _ytdlpExecutableService.GetExecutableVersionAsync();
        var denoVersion = await _denoExecutableService.GetExecutableVersionAsync();
        var ffmpegOutput = await ExecuteAsync("ffmpeg", "-version");
        var ariaOutput = await ExecuteAsync("aria2c", "--version");
        var ffmpegVersion = ParseToolVersion(ffmpegOutput, "ffmpeg version ", "Copyright");
        var ariaVersion = ParseToolVersion(ariaOutput, "aria2 version ", null);
        extraInformation += string.IsNullOrEmpty(extraInformation) ? string.Empty : "\n";
        extraInformation += $"Database encrypted: {_databaseService.IsEncrypted}\nLog path: {(_appInfo.IsPortable ? "app.log" : Path.Combine(UserDirectories.LocalData, _appInfo.Name, "app.log"))}";
        extraInformation += $"\n\nyt-dlp: {(ytdlpVersion is not null ? ytdlpVersion.ToString() : "not found")}";
        extraInformation += $"\ndeno: {(denoVersion is not null ? denoVersion.ToString() : "not found")}";
        extraInformation += $"\nffmpeg: {ffmpegVersion}";
        extraInformation += $"\naria2: {ariaVersion}";
        extraInformation += $"\n\n{await _configurationService.ToStringAsync()}";
        return Desktop.System.Environment.GetDebugInformation(_appInfo, extraInformation);
    }

    public bool PauseDownload(int id)
    {
        try
        {
            return _downloadService.Pause(id);
        }
        catch (Exception e)
        {
            _logger.LogError($"An error occurred while pausing download ({id}): {e}");
            _notificationService.Send(new AppNotification(_translationService._("An error occurred while pausing the download"), NotificationSeverity.Error)
            {
                Action = "error",
                ActionParam = e.ToString()
            });
            return false;
        }
    }

    public async Task RecoverAllDownloadsAsync()
    {
        try
        {
            await _downloadService.RecoverAllAsync();
        }
        catch (Exception e)
        {
            _logger.LogError($"An error occurred while recovering downloads: {e}");
            _notificationService.Send(new AppNotification(_translationService._("An error occurred while recovering downloads"), NotificationSeverity.Error)
            {
                Action = "error",
                ActionParam = e.ToString()
            });
        }
    }

    public bool ResumeDownload(int id)
    {
        try
        {
            return _downloadService.Resume(id);
        }
        catch (Exception e)
        {
            _logger.LogError($"An error occurred while resuming download ({id}): {e}");
            _notificationService.Send(new AppNotification(_translationService._("An error occurred while resuming the download"), NotificationSeverity.Error)
            {
                Action = "error",
                ActionParam = e.ToString()
            });
            return false;
        }
    }

    public async Task RetryFailedDownloadsAsync()
    {
        try
        {
            await _downloadService.RetryFailedAsync();
        }
        catch (Exception e)
        {
            _logger.LogError($"An error occurred while retrying failed downloads: {e}");
            _notificationService.Send(new AppNotification(_translationService._("An error occurred while retrying failed downloads"), NotificationSeverity.Error)
            {
                Action = "error",
                ActionParam = e.ToString()
            });
        }
    }

    public async Task<bool> RetryDownloadAsync(int id)
    {
        try
        {
            return await _downloadService.RetryAsync(id);
        }
        catch (Exception e)
        {
            _logger.LogError($"An error occurred while retrying download ({id}): {e}");
            _notificationService.Send(new AppNotification(_translationService._("An error occurred while retrying the download"), NotificationSeverity.Error)
            {
                Action = "error",
                ActionParam = e.ToString()
            });
            return false;
        }
    }

    public async Task StopAllDownloadsAsync()
    {
        try
        {
            await _downloadService.StopAllAsync();
        }
        catch (Exception e)
        {
            _logger.LogError($"An error occurred while stopping all downloads: {e}");
            _notificationService.Send(new AppNotification(_translationService._("An error occurred while stopping all downloads"), NotificationSeverity.Error)
            {
                Action = "error",
                ActionParam = e.ToString()
            });
        }
    }

    public async Task<bool> StopDownloadAsync(int id)
    {
        try
        {
            return await _downloadService.StopAsync(id);
        }
        catch (Exception e)
        {
            _logger.LogError($"An error occurred while stopping download ({id}): {e}");
            _notificationService.Send(new AppNotification(_translationService._("An error occurred while stopping the download"), NotificationSeverity.Error)
            {
                Action = "error",
                ActionParam = e.ToString()
            });
            return false;
        }
    }

    public async Task WindowsUpdateAsync(IProgress<DownloadProgress> progress)
    {
        var res = await _updaterService.WindowsApplicationUpdateAsync(_latestAppVersion, progress);
        if (res)
        {
            _notificationService.Send(new AppNotification(_translationService._("Starting {0} installer...", _appInfo.ShortName!), NotificationSeverity.Success));
        }
        else
        {
            _notificationService.Send(new AppNotification(_translationService._("Unable to download and install the update"), NotificationSeverity.Error));
        }
    }

    public async Task YtdlpUpdateAsync(IProgress<DownloadProgress> progress)
    {
        var res = await _ytdlpExecutableService.DownloadUpdateAsync(_latestYtdlpVersion, progress);
        if (res)
        {
            _notificationService.Send(new AppNotification(_translationService._("yt-dlp {0} installed successfully", _latestYtdlpVersion.ToString()), NotificationSeverity.Success));
        }
        else
        {
            _notificationService.Send(new AppNotification(_translationService._("Unable to download and install the yt-dlp update"), NotificationSeverity.Error));
        }
    }

    private static string ParseToolVersion(string output, string prefix, string? endMarker)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return "not found";
        }
        var prefixIndex = output.IndexOf(prefix, StringComparison.Ordinal);
        if (prefixIndex >= 0)
        {
            var valueStart = prefixIndex + prefix.Length;
            var lineEnd = output.IndexOfAny(['\r', '\n'], valueStart);
            var markerIndex = endMarker is not null ? output.IndexOf(endMarker, valueStart, StringComparison.Ordinal) : -1;
            var valueEnd = -1;
            if (markerIndex > valueStart && lineEnd > valueStart)
            {
                valueEnd = Math.Min(markerIndex, lineEnd);
            }
            else if (markerIndex > valueStart)
            {
                valueEnd = markerIndex;
            }
            else if (lineEnd > valueStart)
            {
                valueEnd = lineEnd;
            }
            var parsed = (valueEnd > valueStart ? output[valueStart..valueEnd] : output[valueStart..]).Trim();
            if (!string.IsNullOrEmpty(parsed))
            {
                return parsed;
            }
        }
        var firstLineEnd = output.IndexOfAny(['\r', '\n']);
        var firstLine = (firstLineEnd >= 0 ? output[..firstLineEnd] : output).Trim();
        return string.IsNullOrEmpty(firstLine) ? "not found" : firstLine;
    }

    private async Task<string> ExecuteAsync(string executable, string arguments)
    {
        try
        {
            using var process = new Process()
            {
                StartInfo = new ProcessStartInfo(executable, arguments)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            if (!process.Start())
            {
                return string.Empty;
            }
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                var outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);
                await process.WaitForExitAsync(cts.Token);
                var output = await outputTask;
                return process.ExitCode == 0 ? output : string.Empty;
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                }
                return string.Empty;
            }
        }
        catch (Win32Exception)
        {
            return string.Empty;
        }
        catch (FileNotFoundException)
        {
            return string.Empty;
        }
        catch (Exception e)
        {
            _logger.LogDebug($"Failed to execute '{executable} {arguments}': {e.Message}");
            return string.Empty;
        }
    }

    private async void ConfigurationService_Saved(object? sender, ConfigurationSavedEventArgs e)
    {
        if (e.ChangedPropertyName == "PreventSuspend")
        {
            if (_configurationService.PreventSuspend)
            {
                await _powerService.PreventSuspendAsync();
            }
            else
            {
                await _powerService.AllowSuspendAsync();
            }
        }
    }
}
