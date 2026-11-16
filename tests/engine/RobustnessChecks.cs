using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Nickvision.Desktop.Application;
using Nickvision.Desktop.Filesystem;
using Nickvision.Desktop.Globalization;
using Nickvision.Desktop.Keyring;
using Nickvision.Desktop.Notifications;
using Nickvision.Desktop.System;
using Nickvision.Parabolic.Shared.Controllers;
using Nickvision.Parabolic.Shared.Helpers;
using Nickvision.Parabolic.Shared.Models;
using Nickvision.Parabolic.Shared.Services;

internal static class RobustnessChecks
{
    public static async Task RunAsync(IConfigurationService config, ITranslationService translator, string root)
    {
        Require(LaunchUrlParser.FromCommandLine("\"C:\\Program Files\\BearDL.exe\" \"parabolic://example.com/watch?v=1&x=%26#part\"")?.AbsoluteUri
            == "https://example.com/watch?v=1&x=%26#part", "Redirected quoted URL preserves query and fragment");
        Require(LaunchUrlParser.Parse("parabolic://http/example.com/watch?v=1&x=%26#part")?.AbsoluteUri
            == "http://example.com/watch?v=1&x=%26#part", "Protocol parser preserves plain http scheme");
        Require(LaunchUrlParser.Parse("parabolic://https/example.com/watch?v=1")?.AbsoluteUri
            == "https://example.com/watch?v=1", "Protocol parser preserves explicit https scheme");
        Require(LaunchUrlParser.Parse("PARABOLIC://example.com/watch")?.Scheme == "https", "Protocol parsing is case insensitive");
        Require(LaunchUrlParser.FromCommandLine("\"C:\\Program Files\\BearDL.exe\" --portable") is null, "Ordinary reactivation is not a URL");
        Require(LaunchUrlParser.Parse("javascript:alert(1)") is null, "Unsupported launch scheme rejected");
        var batch = Path.Combine(root, "batch.txt");
        Require(LaunchUrlParser.FromCommandLine($"\"C:\\BearDL.exe\" \"{new Uri(batch).AbsoluteUri}\"")?.IsFile == true, "Command line parser accepts file:// URI");
        Require(LaunchUrlParser.FromCommandLine($"\"C:\\BearDL.exe\" \"{batch}\"")?.LocalPath == Path.GetFullPath(batch), "Command line parser accepts .txt batch path");

        var configStore = new ConcurrentDictionary<string, object?>();
        var writableConfig = Proxy.Create<IConfigurationService>((method, values) =>
        {
            if (method.Name == "Get")
            {
                return configStore.TryGetValue(values[0]!.ToString()!, out var existing) ? existing : values[1];
            }
            if (method.Name == "Set")
            {
                configStore[values[0]!.ToString()!] = values[1];
                return null;
            }
            throw new NotSupportedException(method.Name);
        });
        Require(writableConfig.UseModernLayout, "UseModernLayout defaults to true");
        Require(!writableConfig.EmbedThumbnails, "EmbedThumbnails defaults to false");
        writableConfig.UseModernLayout = false;
        Require(!writableConfig.UseModernLayout, "UseModernLayout persists false");
        writableConfig.UseModernLayout = true;
        Require(writableConfig.UseModernLayout, "UseModernLayout restores true");

        var log = new BoundedLog(4096);
        Parallel.For(0, 10000, i => log.Enqueue(new string('x', 200) + i));
        log.Enqueue("last diagnostic");
        Require(log.ToString().Length <= 4096 && log.ToString().EndsWith("last diagnostic"), "Bounded concurrent log tail");
        Require(ReferenceEquals(log.ToString(), log.ToString()), "Unchanged log snapshot reused");
        for (var i = 0; i < 64; i++) { _ = log.ToString(); }
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10000; i++) { _ = log.ToString(); }
        Require(GC.GetAllocatedBytesForCurrentThread() - allocatedBefore < 65536 && ReferenceEquals(log.ToString(), log.ToString()), "Repeated log reads do not allocate full snapshots");
        log.Enqueue(new string('y', 20000));
        Require(log.ToString().Length <= 4096, "Oversized diagnostic bounded");

        var handler = new CountingHttpMessageHandler();
        var httpFactory = Proxy.Create<IHttpClientFactory>((method, _) =>
            method.Name == "CreateClient" ? new HttpClient(handler, false) : throw new NotSupportedException(method.Name));
        var thumbnailService = new ThumbnailService(NullLogger<ThumbnailService>.Instance, httpFactory);
        var sharedUrl = new Uri("https://example.com/shared.jpg");
        handler.GateUrl = sharedUrl;
        handler.GateEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.GateRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fetchA = thumbnailService.GetImageBytesAsync(sharedUrl);
        var fetchB = thumbnailService.GetImageBytesAsync(sharedUrl);
        await handler.GateEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        handler.GateRelease.SetResult();
        var sharedBytes = await Task.WhenAll(fetchA, fetchB);
        Require(sharedBytes[0].SequenceEqual(sharedBytes[1]) && handler.Counts[sharedUrl] == 1, "ThumbnailService deduplicates concurrent identical fetches");
        handler.GateUrl = null;
        for (var i = 0; i < 98; i++)
        {
            _ = await thumbnailService.GetImageBytesAsync(new Uri($"https://example.com/thumb-{i}.jpg"));
        }
        var newestThumb = new Uri("https://example.com/thumb-97.jpg");
        var evictedThumb = new Uri("https://example.com/thumb-0.jpg");
        _ = await thumbnailService.GetImageBytesAsync(newestThumb);
        _ = await thumbnailService.GetImageBytesAsync(evictedThumb);
        Require(handler.Counts[newestThumb] == 1 && handler.Counts[evictedThumb] == 2, "ThumbnailService bounds cache via LRU eviction");

        var denoStub = Proxy.Create<IDenoExecutableService>((method, _) =>
            method.Name == "get_ExecutablePath" ? null : throw new NotSupportedException(method.Name));
        var ytdlpService = new YtdlpExecutableService(
            NullLogger<YtdlpExecutableService>.Instance,
            NullLogger<UpdaterService>.Instance,
            writableConfig,
            denoStub,
            httpFactory);
        using (var defaultMp4 = ytdlpService.GetDownloadProcess(new DownloadOptions(new Uri("https://example.com/v"))
        {
            SaveFolder = root,
            SaveFilename = "thumb-default",
            FileType = MediaFileType.MP4
        }))
        {
            Require(!defaultMp4.StartInfo.ArgumentList.Contains("--write-thumbnail")
                && !defaultMp4.StartInfo.ArgumentList.Contains("--embed-thumbnail")
                && !defaultMp4.StartInfo.ArgumentList.Contains("--convert-thumbnails"),
                "Default video download does not download or write thumbnail images");
        }
        writableConfig.EmbedThumbnails = true;
        using (var genericVideo = ytdlpService.GetDownloadProcess(new DownloadOptions(new Uri("https://example.com/v"))
        {
            SaveFolder = root,
            SaveFilename = "thumb-generic",
            FileType = MediaFileType.Video
        }))
        {
            Require(!genericVideo.StartInfo.ArgumentList.Contains("--write-thumbnail")
                && !genericVideo.StartInfo.ArgumentList.Contains("--embed-thumbnail")
                && !genericVideo.StartInfo.ArgumentList.Contains("--convert-thumbnails"),
                "Unsupported video types never fall back to saving separate thumbnail files");
        }
        using (var embeddedMp4 = ytdlpService.GetDownloadProcess(new DownloadOptions(new Uri("https://example.com/v"))
        {
            SaveFolder = root,
            SaveFilename = "thumb-mp4",
            FileType = MediaFileType.MP4
        }))
        {
            Require(embeddedMp4.StartInfo.ArgumentList.Contains("--embed-thumbnail")
                && embeddedMp4.StartInfo.ArgumentList.Contains("--convert-thumbnails")
                && !embeddedMp4.StartInfo.ArgumentList.Contains("--write-thumbnail"),
                "Supported types embed thumbnails without writing separate image files when enabled");
        }
        writableConfig.EmbedThumbnails = false;

        var calls = 0;
        var executable = Proxy.Create<IYtdlpExecutableService>((method, values) =>
        {
            if (method.Name == "GetDiscoveryProcessArguments") { return new[] { values[0]!.ToString()! }; }
            if (method.Name == "ExecuteAsync")
            {
                ((CancellationToken)values[^1]!).ThrowIfCancellationRequested();
                Interlocked.Increment(ref calls);
                var url = ((IReadOnlyList<string>)values[0]!)[0];
                return ExecuteDiscoveryStubAsync(url);
            }
            throw new NotSupportedException(method.Name);
        });
        var thumbnails = Proxy.Create<IThumbnailService>((_, _) => null);
        var discovery = new DiscoveryService(NullLogger<DiscoveryService>.Instance, config, thumbnails, translator, executable);
        await File.WriteAllLinesAsync(batch, new[] { "https://example.com/invalid", "https://example.com/valid", "https://example.com/null" });
        var result = await discovery.GetForBatchFileAsync(batch);
        Require(result.Media.Count == 1 && result.FailedUrls.Count == 2, "Batch preserves valid entries and reports failures");
        var orderedUrls = new[]
        {
            "https://example.com/slow-0",
            "https://example.com/fast-1",
            "https://example.com/fast-2",
            "https://example.com/fast-3",
            "https://example.com/fast-4",
            "https://example.com/fast-5"
        };
        await File.WriteAllLinesAsync(batch, orderedUrls);
        var orderedResult = await discovery.GetForBatchFileAsync(batch);
        Require(orderedResult.Media.Select(m => m.Title).SequenceEqual(new[] { "slow-0", "fast-1", "fast-2", "fast-3", "fast-4", "fast-5" }),
            "Bounded parallel batch discovery preserves original input order");
        Require((await discovery.GetForUrlAsync(new Uri("https://example.com/empty"))).Media.Count == 0, "Empty playlist stays empty");
        await File.WriteAllLinesAsync(batch, new[] { "https://example.com/invalid", "https://example.com/null" });
        result = await discovery.GetForBatchFileAsync(batch);
        Require(result.Media.Count == 0 && result.FailedUrls.Count == 2, "All failed entries remain reported");
        var nested = """{"title":"leaf"}""";
        using (var emptyNested = System.Text.Json.JsonDocument.Parse("""{"entries":[{"entries":[]}]}"""))
        {
            Require(new DiscoveryResult(emptyNested.RootElement, config, translator, new Uri("https://example.com/empty-nested"), root, "").Media.Count == 0, "Nested empty playlist does not become a download");
        }
        for (var i = 0; i < 18; i++) { nested = "{\"entries\":[" + nested + "]}"; }
        using (var document = System.Text.Json.JsonDocument.Parse(nested))
        {
            try
            {
                _ = new DiscoveryResult(document.RootElement, config, translator, new Uri("https://example.com/deep"), root, "");
                throw new Exception("Deep nesting accepted");
            }
            catch (YtdlpException) { }
        }
        calls = 0;
        try { await discovery.GetForUrlAsync(new Uri("https://example.com/cycle")); throw new Exception("Cycle accepted"); }
        catch (YtdlpException) { Require(calls == 1, "Cycle rejected before repeated extraction"); }
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        try { await discovery.GetForBatchFileAsync(batch, cancellationToken: cancelled.Token); throw new Exception("Cancellation swallowed"); }
        catch (OperationCanceledException) { }

        var progressExecutable = Proxy.Create<IYtdlpExecutableService>((method, args) =>
        {
            if (method.Name != "GetDownloadProcess") { throw new NotSupportedException(method.Name); }
            var downloadOptions = (DownloadOptions)args[0]!;
            var info = new ProcessStartInfo(System.Environment.ProcessPath!)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            if (Path.GetFileNameWithoutExtension(System.Environment.ProcessPath!).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                info.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            }
            info.ArgumentList.Add("--child");
            info.ArgumentList.Add("progress");
            info.ArgumentList.Add(Path.Combine(downloadOptions.SaveFolder, downloadOptions.SaveFilename + ".actual"));
            return new Process { StartInfo = info, EnableRaisingEvents = true };
        });
        var progressOptions = new DownloadOptions(new Uri("https://example.com/progress"))
        {
            SaveFolder = root,
            SaveFilename = "progress-check",
            FileType = MediaFileType.Video
        };
        using (var progressDownload = new Download(config, translator, progressExecutable, progressOptions))
        {
            var sawParsedProgress = false;
            var nonProgressEvents = 0;
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            progressDownload.ProgressChanged += (_, args) =>
            {
                if (Math.Abs(args.Progress - 0.25) < 0.001 && Math.Abs(args.Speed - 100.0) < 0.01 && args.Eta == 2)
                {
                    sawParsedProgress = true;
                }
                else if (double.IsNaN(args.Progress))
                {
                    Interlocked.Increment(ref nonProgressEvents);
                }
            };
            progressDownload.Completed += (_, _) => done.TrySetResult();
            progressDownload.Start();
            await done.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Require(progressDownload.Status == DownloadStatus.Success && sawParsedProgress, "Span progress parser handles Parabolic progress fields");
            Require(progressDownload.Log.Contains("burst line 0") && progressDownload.Log.Contains("burst line 39"), "Throttling preserves full diagnostic log");
            Require(nonProgressEvents < 40, "Burst non-progress output is throttled");
        }

        // Real database and RecoveryService, including rollback and reopening from disk.
        var name = "BearDL.RecoveryChecks." + Guid.NewGuid().ToString("N");
        var app = new AppInfo("org.beardl.recoverychecks", name, "Recovery checks");
        var secret = Proxy.Create<ISecretService>((_, _) => throw new InvalidOperationException("No system keyring in tests"));
        var folder = Path.Combine(UserDirectories.Config, name);
        try
        {
            using (var database = new DatabaseService(NullLogger<DatabaseService>.Instance, app, secret))
            {
                var recovery = new RecoveryService(NullLogger<RecoveryService>.Instance, config, database);
                var options = new DownloadOptions(new Uri("https://example.com/recover")) { SaveFolder = root, SaveFilename = "recover", ExcludeFromHistory = true };
                Require(await recovery.AddAsync(new RecoverableDownload(1, options)), "Persist recovery record");
                try
                {
                    await recovery.ReplaceAsync(new[] { 1 }, new[] { new RecoverableDownload(2, options), new RecoverableDownload(2, options) });
                    throw new Exception("Duplicate primary key accepted");
                }
                catch (SqliteException) { }
                var records = await recovery.GetAllAsync();
                Require(records.Count == 1 && records[0].Id == 1, "Failed replacement rolls back deletes and inserts");
                Require(await recovery.ReplaceAsync(new[] { 1 }, new[] { new RecoverableDownload(3, options) }), "Commit replacement");
            }
            using (var reopened = new DatabaseService(NullLogger<DatabaseService>.Instance, app, secret))
            {
                var recovery = new RecoveryService(NullLogger<RecoveryService>.Instance, config, reopened);
                var records = await recovery.GetAllAsync();
                Require(records.Count == 1 && records[0].Id == 3 && records[0].Options.ExcludeFromHistory, "Recovery survives database reopen");
            }
        }
        finally
        {
            if (Directory.Exists(folder)) { Directory.Delete(folder, true); }
        }
        // AddDownloadDialogController ExportDescription defaults and non-persistence.
        var controllerRoot = Path.Combine(root, "export-description-checks");
        Directory.CreateDirectory(controllerRoot);
        try
        {
            var controllerConfigStore = new ConcurrentDictionary<string, object?>();
            controllerConfigStore["ExportDescription"] = true;
            var exportDescriptionSetCalls = 0;
            using var txConnection = new SqliteConnection("Data Source=:memory:");
            txConnection.Open();
            var controllerConfig = Proxy.Create<IConfigurationService>((method, values) =>
            {
                if (method.Name == "Get")
                {
                    return controllerConfigStore.TryGetValue(values[0]!.ToString()!, out var existing) ? existing : values[1];
                }
                if (method.Name == "Set")
                {
                    var key = values[0]!.ToString()!;
                    if (key == "ExportDescription")
                    {
                        Interlocked.Increment(ref exportDescriptionSetCalls);
                    }
                    controllerConfigStore[key] = values[1];
                    return null;
                }
                if (method.Name == "CreateTransactionAsync")
                {
                    return Task.FromResult(txConnection.BeginTransaction());
                }
                throw new NotSupportedException(method.Name);
            });
            var controllerYtdlpService = new YtdlpExecutableService(
                NullLogger<YtdlpExecutableService>.Instance,
                NullLogger<UpdaterService>.Instance,
                controllerConfig,
                denoStub,
                httpFactory);
            using var fastDoc = System.Text.Json.JsonDocument.Parse("""{"title":"Fast Item","webpage_url":"https://example.com/fast-item"}""");
            var fastDiscoveryResult = new DiscoveryResult(fastDoc.RootElement, controllerConfig, translator, new Uri("https://example.com/fast-item"), string.Empty, string.Empty);
            var controllerDiscovery = Proxy.Create<IDiscoveryService>((method, _) =>
                method.Name == "GetForUrlAsync"
                    ? Task.FromResult(fastDiscoveryResult)
                    : throw new NotSupportedException(method.Name));
            var capturedBatches = new List<IReadOnlyList<DownloadOptions>>();
            var controllerDownloadService = Proxy.Create<IDownloadService>((method, args) =>
            {
                if (method.Name == "AddWithDuplicateCheckAsync")
                {
                    capturedBatches.Add((IReadOnlyList<DownloadOptions>)args[0]!);
                    return Task.FromResult(true);
                }
                throw new NotSupportedException(method.Name);
            });
            var notifications = new List<AppNotification>();
            var controllerNotifications = Proxy.Create<INotificationService>((method, args) =>
            {
                if (method.Name == "Send")
                {
                    notifications.Add((AppNotification)args[0]!);
                    return null;
                }
                throw new NotSupportedException(method.Name);
            });
            var controllerLifetime = Proxy.Create<IHostApplicationLifetime>((method, _) =>
                method.Name == "get_ApplicationStopping" ? CancellationToken.None : throw new NotSupportedException(method.Name));
            var controllerKeyring = Proxy.Create<IKeyringService>((method, _) =>
                method.Name == "GetAllCredentialAsync"
                    ? Task.FromResult<IReadOnlyList<Credential>>(Array.Empty<Credential>())
                    : throw new NotSupportedException(method.Name));
            var controller = new AddDownloadDialogController(
                NullLogger<AddDownloadDialogController>.Instance,
                controllerLifetime,
                controllerConfig,
                controllerDiscovery,
                controllerDownloadService,
                controllerKeyring,
                controllerNotifications,
                thumbnails,
                translator);

            Require(!controller.PreviousExportDescription, "PreviousExportDescription returns false even when config has ExportDescription=true");

            await controller.AddFastDownloadsAsync(new Uri("https://example.com/fast-item"), null, controllerRoot);
            Require(capturedBatches.Count == 1 && capturedBatches[0].Count == 1, "Fast download queues expected item");
            Require(!capturedBatches[0][0].ExportDescription, "AddFastDownloadsAsync sets ExportDescription=false");
            using (var fastProcess = controllerYtdlpService.GetDownloadProcess(capturedBatches[0][0]))
            {
                Require(!fastProcess.StartInfo.ArgumentList.Contains("--write-description"), "Fast download process omits --write-description");
            }

            using var singleDoc = System.Text.Json.JsonDocument.Parse("""{"title":"Single Item","webpage_url":"https://example.com/single-item"}""");
            var singleMedia = new Media(singleDoc.RootElement, controllerConfig, translator, string.Empty, string.Empty, new Uri("https://example.com/single-item"));
            var singleContext = new DiscoveryContext(101, new Uri("https://example.com/single-item"), "Single Item", null, new[] { singleMedia });
            await controller.AddSingleDownloadAsync(
                singleContext,
                "single-item",
                controllerRoot,
                new SelectionItem<MediaFileType>(MediaFileType.MP4, "MP4", true),
                new SelectionItem<Format>(Format.BestVideo, "Best", true),
                new SelectionItem<Format>(Format.BestAudio, "Best", true),
                Array.Empty<SelectionItem<SubtitleLanguage>>(),
                splitChapters: false,
                exportDescription: true,
                excludeFromHistory: false,
                new SelectionItem<PostProcessorArgument?>(null, "None", true),
                string.Empty,
                string.Empty);
            Require(capturedBatches.Count == 2 && capturedBatches[1].Count == 1, "Single download queues expected item");
            Require(capturedBatches[1][0].ExportDescription, "AddSingleDownloadAsync honors explicit exportDescription=true");
            using (var singleProcess = controllerYtdlpService.GetDownloadProcess(capturedBatches[1][0]))
            {
                Require(singleProcess.StartInfo.ArgumentList.Contains("--write-description"), "Single download with exportDescription=true includes --write-description");
            }
            Require(!controller.PreviousExportDescription, "AddSingleDownloadAsync leaves PreviousExportDescription=false");

            await controller.AddFastDownloadsAsync(new Uri("https://example.com/fast-item"), null, controllerRoot);
            Require(capturedBatches.Count == 3 && capturedBatches[2].Count == 1, "Subsequent fast download queues expected item");
            Require(!capturedBatches[2][0].ExportDescription, "Subsequent AddFastDownloadsAsync still sets ExportDescription=false");

            using var playlistDoc = System.Text.Json.JsonDocument.Parse("""
                {
                  "title": "MyPlaylist",
                  "entries": [
                    {"title":"Item 1","webpage_url":"https://example.com/playlist-1","playlist_index":1},
                    {"title":"Item 2","webpage_url":"https://example.com/playlist-2","playlist_index":2}
                  ]
                }
                """);
            var playlistDiscovery = new DiscoveryResult(playlistDoc.RootElement, controllerConfig, translator, new Uri("https://example.com/playlist"), string.Empty, string.Empty);
            var playlistContext = new DiscoveryContext(102, playlistDiscovery.Url, playlistDiscovery.Title, null, playlistDiscovery.Media);
            var playlistItems = playlistDiscovery.Media.Select((m, index) => new MediaSelectionItem(index, m, translator)).ToList();
            Directory.CreateDirectory(Path.Combine(controllerRoot, playlistContext.Title.SanitizeForFilename(controllerConfig.LimitCharacters)));
            await controller.AddPlaylistDownloadsAsync(
                playlistContext,
                playlistItems,
                controllerRoot,
                new SelectionItem<MediaFileType>(MediaFileType.MP4, "MP4", true),
                new SelectionItem<VideoResolution>(VideoResolution.Best, "Best", true),
                new SelectionItem<double>(double.MaxValue, "Best", true),
                reverseDownloadOrder: false,
                numberTitles: false,
                Array.Empty<SelectionItem<SubtitleLanguage>>(),
                exportM3U: true,
                splitChapters: false,
                exportDescription: false,
                excludeFromHistory: false,
                new SelectionItem<PostProcessorArgument?>(null, "None", true));
            Require(capturedBatches.Count == 4 && capturedBatches[3].Count == 2, "Playlist download queues all selected items");
            Require(capturedBatches[3].All(item => !item.ExportDescription), "AddPlaylistDownloadsAsync sets ExportDescription=false even when exportM3U=true");
            Require(!controller.PreviousExportDescription, "AddPlaylistDownloadsAsync leaves PreviousExportDescription=false");
            Require(exportDescriptionSetCalls == 0, "Controller never persists PreviousExportDescription");
            Require(!notifications.Any(n => n.Severity == NotificationSeverity.Error), "Controller regression checks produced no error notifications");
        }
        finally
        {
            if (Directory.Exists(controllerRoot))
            {
                Directory.Delete(controllerRoot, true);
            }
        }
        Console.WriteLine("PASS: bounded logs, discovery resilience, SQLite rollback and reopen");
    }

    private static async Task<ProcessResult> ExecuteDiscoveryStubAsync(string url)
    {
        if (url.Contains("slow-0"))
        {
            await Task.Delay(60);
        }
        var output = url.Contains("cycle")
            ? """{"entries":[{"ie_key":"YoutubeTab","url":"https://example.com/cycle"}]}"""
            : url.Contains("invalid") ? "{broken"
            : url.Contains("null") ? """{"entries":null}"""
            : url.Contains("empty") ? """{"entries":[]}"""
            : url.Contains("slow-0") ? """{"title":"slow-0","webpage_url":"https://example.com/slow-0"}"""
            : url.Contains("fast-") ? "{\"title\":\"" + url[(url.LastIndexOf('/') + 1)..] + "\",\"webpage_url\":\"" + url + "\"}"
            : """{"title":"valid","webpage_url":"https://example.com/valid"}""";
        return new ProcessResult(0, output, "");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) { throw new Exception(message); }
    }

    private sealed class CountingHttpMessageHandler : HttpMessageHandler
    {
        public readonly ConcurrentDictionary<Uri, int> Counts = new();
        public Uri? GateUrl;
        public TaskCompletionSource? GateEntered;
        public TaskCompletionSource? GateRelease;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            Counts.AddOrUpdate(uri, 1, (_, existing) => existing + 1);
            if (GateUrl == uri && GateEntered is not null && GateRelease is not null)
            {
                GateEntered.TrySetResult();
                await GateRelease.Task.WaitAsync(cancellationToken);
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(uri.ToString()))
            };
        }
    }
}
