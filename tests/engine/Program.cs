using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Nickvision.Desktop.Application;
using Nickvision.Desktop.Globalization;
using Nickvision.Parabolic.Shared.Helpers;
using Nickvision.Parabolic.Shared.Models;
using Nickvision.Parabolic.Shared.Services;
using MediaFormat = Nickvision.Parabolic.Shared.Models.Format;

if (args.Length > 0 && args[0] == "--child")
{
    if (args[1] == "hold")
    {
        await Task.Delay(TimeSpan.FromMinutes(2));
        return;
    }
    var path = args[2];
    await File.WriteAllTextAsync(path, "media");
    // Exercise concurrent stderr and stdout, followed by the final output marker.
    await Task.WhenAll(Task.Run(() =>
    {
        for (var i = 0; i < 500; i++) { Console.Error.WriteLine("diagnostic " + i); }
    }), Task.Run(() =>
    {
        for (var i = 0; i < 500; i++) { Console.WriteLine("output " + i); }
    }));
    Console.WriteLine("__BEARDL_FILE__" + path);
    Console.Error.WriteLine("trailing diagnostic");
    return;
}

var root = Path.Combine(Path.GetTempPath(), "beardl-engine-" + Guid.NewGuid());
Directory.CreateDirectory(root);
try
{
    Check("https://example.com/video description".TryExtractHttpUri(out var uri)
        && uri.AbsoluteUri == "https://example.com/video", "URL before share description");
    Check("description https://example.com/a%20b trailing".TryExtractHttpUri(out uri)
        && uri.AbsoluteUri == "https://example.com/a%20b", "Encoded spaces preserved");

    var config = Proxy.Create<IConfigurationService>((method, values) =>
    {
        if (method.Name == "Get")
        {
            return values[0]?.ToString() switch
            {
                "MaxNumberOfActiveDownloads" => 1,
                "WarnBeforeDuplicateDownload" => true,
                "RemoveSourceData" => false,
                _ => values[^1]
            };
        }
        throw new NotSupportedException(method.Name);
    });
    var translator = Proxy.Create<ITranslationService>((method, values) =>
        method.ReturnType == typeof(string) ? values.FirstOrDefault(value => value is string) ?? "" : null);

    using (var json = JsonDocument.Parse("""{"format_id":"v","ext":"ogg","vcodec":"none","acodec":"vorbis"}"""))
    {
        var format = new MediaFormat(json.RootElement, translator);
        Check(format.AudioCodec == AudioCodec.Vorbis && format.ToString().Contains("Vorbis"), "Vorbis identity and label");
    }
    using (var json = JsonDocument.Parse("""
        {"entries":[
          {"webpage_url":"https://example.com/inner-a","entries":[{"title":"a","playlist_index":1}]},
          {"webpage_url":"https://example.com/inner-b","entries":[{"title":"b","playlist_index":1}]}
        ]}
        """))
    {
        var result = new DiscoveryResult(json.RootElement, config, translator, new Uri("https://example.com/outer"), root, "");
        Check(result.Media.Count == 2 && result.Media[0].Url.AbsoluteUri.EndsWith("inner-a")
            && result.Media[1].Url.AbsoluteUri.EndsWith("inner-b")
            && result.Media.All(item => item.RequiresPlaylistItems && item.PlaylistPosition == 1), "Nested playlist scope");
    }
    var options = new DownloadOptions(new Uri("https://example.com/path")) { SaveFilename = "name" };
    var longFolder = Path.Combine(root, new string('x', OperatingSystem.IsWindows() ? 300 : 4200));
    try
    {
        options.SaveFolder = longFolder;
        throw new Exception("Expected path validation failure");
    }
    catch (PathTooLongException)
    {
        Check(options.SaveFolder == longFolder, "Long folder is never silently replaced");
    }

    using var fixture = new Fixture(config, translator, root);
    var ids = new ConcurrentQueue<int>();
    fixture.Service.DownloadAdded += (_, e) => ids.Enqueue(e.Id);
    await fixture.Service.AddAsync(new[] { fixture.Options("hold-a"), fixture.Options("hold-b") }, false);
    Check(fixture.Service.DownloadingCount == 1 && fixture.Service.QueuedCount == 1, "Active limit enforced");
    await fixture.Service.StopAsync(ids.First());
    Check(fixture.Service.DownloadingCount == 1 && fixture.Service.QueuedCount == 0 && fixture.Starts.Count == 2, "Stop advances queue");
    await fixture.Service.AddAsync(fixture.Options("hold-c"), false);
    var cleared = await fixture.Service.ClearQueuedAsync();
    Check(cleared.Count == 1 && !fixture.Recovery.ContainsKey(cleared[0]), "Clear removes persisted queue");
    await fixture.Service.StopAllAsync();

    var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    fixture.Service.DownloadCompleted += (_, _) => completion.TrySetResult();
    await fixture.Service.AddAsync(fixture.Options("finish"), false);
    await completion.Task.WaitAsync(TimeSpan.FromSeconds(20));
    Check(fixture.History.TryGetValue(new Uri("https://example.com/finish"), out var history)
        && history.Path.EndsWith("finish.actual") && File.Exists(history.Path), "History uses drained final output path");

    using var excluded = new Fixture(config, translator, root);
    var excludedDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    excluded.Service.DownloadCompleted += (_, _) => excludedDone.TrySetResult();
    await excluded.Service.AddAsync(excluded.Options("excluded"), true);
    await excludedDone.Task.WaitAsync(TimeSpan.FromSeconds(20));
    Check(excluded.History.IsEmpty, "History exclusion survives completion");

    using var recovery = new Fixture(config, translator, root);
    recovery.Recovery[90000] = new RecoverableDownload(90000, recovery.Options("hold-recovered"));
    recovery.FailReplace = true;
    try
    {
        await recovery.Service.RecoverAllAsync();
        throw new Exception("Expected persistence failure");
    }
    catch (IOException) { }
    Check(recovery.Recovery.ContainsKey(90000) && recovery.Starts.IsEmpty, "Failed recovery preserves old rows and starts nothing");
    recovery.FailReplace = false;
    await recovery.Service.RecoverAllAsync();
    Check(!recovery.Recovery.ContainsKey(90000) && recovery.Recovery.Count == 1 && recovery.Starts.Count == 1, "Successful recovery replaces old rows");
    await recovery.Service.StopAllAsync();

    using var concurrent = new Fixture(config, translator, root);
    await Task.WhenAll(Enumerable.Range(0, 20).Select(i => concurrent.Service.AddAsync(concurrent.Options("hold-" + i), false)));
    Check(concurrent.Service.DownloadingCount == 1 && concurrent.Service.QueuedCount == 19, "Concurrent additions respect capacity");
    var confirmEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var confirmResult = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var addition = concurrent.Service.AddWithDuplicateCheckAsync(new[] { concurrent.Options("hold-0") }, false, _ =>
    {
        confirmEntered.TrySetResult();
        return confirmResult.Task;
    });
    await confirmEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
    await concurrent.Service.StopAllAsync().WaitAsync(TimeSpan.FromSeconds(5));
    confirmResult.SetResult(false);
    Check(!await addition && concurrent.Service.DownloadingCount == 0, "Confirmation does not hold state gate");
    Console.WriteLine("Engine regression checks passed.");
}
finally
{
    // Child processes are stopped by the fixtures before removing test artifacts.
    Directory.Delete(root, true);
}

static void Check(bool condition, string message)
{
    if (!condition) { throw new Exception(message); }
    Console.WriteLine("PASS: " + message);
}

public class Proxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Create<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, Proxy>();
        ((Proxy)(object)proxy).Handler = handler;
        return proxy;
    }
}

sealed class Fixture : IDisposable
{
    public readonly ConcurrentDictionary<int, RecoverableDownload> Recovery = new();
    public readonly ConcurrentDictionary<Uri, HistoricDownload> History = new();
    public readonly ConcurrentQueue<string> Starts = new();
    public bool FailReplace;
    public DownloadService Service { get; }
    private readonly string _root;

    public Fixture(IConfigurationService config, ITranslationService translator, string root)
    {
        _root = root;
        var recovery = Proxy.Create<IRecoveryService>((method, args) =>
        {
            switch (method.Name)
            {
                case "GetAllAsync": return Task.FromResult<IReadOnlyList<RecoverableDownload>>(Recovery.Values.ToList());
                case "AddAsync":
                    var added = args[0] is RecoverableDownload single ? new[] { single } : (IReadOnlyList<RecoverableDownload>)args[0]!;
                    foreach (var record in added) { Recovery[record.Id] = record; }
                    return Task.FromResult(true);
                case "ReplaceAsync":
                    if (FailReplace) { return Task.FromResult(false); }
                    foreach (var id in (IReadOnlyList<int>)args[0]!) { Recovery.TryRemove(id, out _); }
                    foreach (var record in (IReadOnlyList<RecoverableDownload>)args[1]!) { Recovery[record.Id] = record; }
                    return Task.FromResult(true);
                case "RemoveAsync":
                    var ids = args[0] is int idValue ? new[] { idValue } : (IReadOnlyList<int>)args[0]!;
                    foreach (var id in ids) { Recovery.TryRemove(id, out _); }
                    return Task.FromResult(true);
                default: throw new NotSupportedException(method.Name);
            }
        });
        var history = Proxy.Create<IHistoryService>((method, args) =>
        {
            if (method.Name == "GetSavedFilesAsync")
            {
                return Task.FromResult<IReadOnlyDictionary<Uri, string>>(History.ToDictionary(pair => pair.Key, pair => pair.Value.Path));
            }
            if (method.Name == "AddAsync")
            {
                var item = (HistoricDownload)args[0]!;
                History[item.Url] = item;
                return Task.FromResult(true);
            }
            throw new NotSupportedException(method.Name);
        });
        var ytdlp = Proxy.Create<IYtdlpExecutableService>((method, args) =>
        {
            if (method.Name != "GetDownloadProcess") { throw new NotSupportedException(method.Name); }
            var options = (DownloadOptions)args[0]!;
            Starts.Enqueue(options.SaveFilename);
            var info = new ProcessStartInfo(Environment.ProcessPath!)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            if (Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                info.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            }
            info.ArgumentList.Add("--child");
            info.ArgumentList.Add(options.SaveFilename.StartsWith("hold") ? "hold" : "finish");
            info.ArgumentList.Add(Path.Combine(root, options.SaveFilename + ".actual"));
            return new Process { StartInfo = info, EnableRaisingEvents = true };
        });
        Service = new DownloadService(NullLogger<DownloadService>.Instance, config, history, recovery, translator, ytdlp);
    }

    public DownloadOptions Options(string name) => new(new Uri("https://example.com/" + name))
    {
        SaveFolder = _root,
        SaveFilename = name,
        FileType = MediaFileType.Video
    };

    public void Dispose() => Service.Dispose();
}

