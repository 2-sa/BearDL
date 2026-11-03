using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Nickvision.Desktop.Application;
using Nickvision.Desktop.Filesystem;
using Nickvision.Desktop.Globalization;
using Nickvision.Desktop.System;
using Nickvision.Parabolic.Shared.Helpers;
using Nickvision.Parabolic.Shared.Models;
using Nickvision.Parabolic.Shared.Services;

internal static class RobustnessChecks
{
    public static async Task RunAsync(IConfigurationService config, ITranslationService translator, string root)
    {
        var log = new BoundedLog(4096);
        Parallel.For(0, 10000, i => log.Enqueue(new string('x', 200) + i));
        log.Enqueue("last diagnostic");
        Require(log.ToString().Length <= 4096 && log.ToString().EndsWith("last diagnostic"), "Bounded concurrent log tail");
        Require(ReferenceEquals(log.ToString(), log.ToString()), "Unchanged log snapshot reused");
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10000; i++) { _ = log.ToString(); }
        Require(GC.GetAllocatedBytesForCurrentThread() - allocatedBefore < 1024, "Repeated log reads do not allocate full snapshots");
        log.Enqueue(new string('y', 20000));
        Require(log.ToString().Length <= 4096, "Oversized diagnostic bounded");

        var calls = 0;
        var executable = Proxy.Create<IYtdlpExecutableService>((method, values) =>
        {
            if (method.Name == "GetDiscoveryProcessArguments") { return new[] { values[0]!.ToString()! }; }
            if (method.Name == "ExecuteAsync")
            {
                ((CancellationToken)values[^1]!).ThrowIfCancellationRequested();
                calls++;
                var url = ((IReadOnlyList<string>)values[0]!)[0];
                var output = url.Contains("cycle")
                    ? """{"entries":[{"ie_key":"YoutubeTab","url":"https://example.com/cycle"}]}"""
                    : url.Contains("invalid") ? "{broken"
                    : url.Contains("null") ? """{"entries":null}"""
                    : url.Contains("empty") ? """{"entries":[]}"""
                    : """{"title":"valid","webpage_url":"https://example.com/valid"}""";
                return Task.FromResult(new ProcessResult(0, output, ""));
            }
            throw new NotSupportedException(method.Name);
        });
        var thumbnails = Proxy.Create<IThumbnailService>((_, _) => null);
        var discovery = new DiscoveryService(NullLogger<DiscoveryService>.Instance, config, thumbnails, translator, executable);
        var batch = Path.Combine(root, "batch.txt");
        await File.WriteAllLinesAsync(batch, new[] { "https://example.com/invalid", "https://example.com/valid", "https://example.com/null" });
        var result = await discovery.GetForBatchFileAsync(batch);
        Require(result.Media.Count == 1 && result.FailedUrls.Count == 2, "Batch preserves valid entries and reports failures");
        Require((await discovery.GetForUrlAsync(new Uri("https://example.com/empty"))).Media.Count == 0, "Empty playlist stays empty");
        await File.WriteAllLinesAsync(batch, new[] { "https://example.com/invalid", "https://example.com/null" });
        result = await discovery.GetForBatchFileAsync(batch);
        Require(result.Media.Count == 0 && result.FailedUrls.Count == 2, "All failed entries remain reported");
        var nested = """{"title":"leaf"}""";
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
        Console.WriteLine("PASS: bounded logs, discovery resilience, SQLite rollback and reopen");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) { throw new Exception(message); }
    }
}
