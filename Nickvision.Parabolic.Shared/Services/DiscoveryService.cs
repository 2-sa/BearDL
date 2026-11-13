using Microsoft.Extensions.Logging;
using Nickvision.Desktop.Application;
using Nickvision.Desktop.Filesystem;
using Nickvision.Desktop.Globalization;
using Nickvision.Desktop.Keyring;
using Nickvision.Parabolic.Shared.Helpers;
using Nickvision.Parabolic.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Nickvision.Parabolic.Shared.Services;

public class DiscoveryService : IDiscoveryService
{
    private static readonly char BatchFileDelimiter;

    private readonly ILogger<DiscoveryService> _logger;
    private readonly IConfigurationService _configurationService;
    private readonly IThumbnailService _thumbnailService;
    private readonly ITranslationService _translationService;
    private readonly IYtdlpExecutableService _ytdlpExecutableService;

    static DiscoveryService()
    {
        BatchFileDelimiter = '|';
    }

    public DiscoveryService(ILogger<DiscoveryService> logger, IConfigurationService configurationService, IThumbnailService thumbnailService, ITranslationService translationService, IYtdlpExecutableService ytdlpExecutableService)
    {
        _logger = logger;
        _configurationService = configurationService;
        _thumbnailService = thumbnailService;
        _translationService = translationService;
        _ytdlpExecutableService = ytdlpExecutableService;
    }

    public async Task<DiscoveryResult> GetForBatchFileAsync(string path, Credential? credential = null, CancellationToken cancellationToken = default)
    {
        var entries = await ParseBatchFileAsync(path, cancellationToken);
        if (entries.Count == 0)
        {
            throw new ArgumentException("The batch file is empty or is of invalid syntax.");
        }
        var entryInfos = new DiscoveryResult?[entries.Count];
        var failedUrls = new string?[entries.Count];
        using var semaphore = new SemaphoreSlim(4, 4);
        var tasks = new Task[entries.Count];
        for (var i = 0; i < entries.Count; i++)
        {
            tasks[i] = DiscoverBatchEntryAsync(i, entries[i]);
        }
        await Task.WhenAll(tasks).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var orderedInfos = new List<DiscoveryResult>(entries.Count);
        var orderedFailures = new List<string>();
        for (var i = 0; i < entries.Count; i++)
        {
            if (entryInfos[i] is { } info)
            {
                orderedInfos.Add(info);
            }
            if (failedUrls[i] is { } failure)
            {
                orderedFailures.Add(failure);
            }
        }
        var result = new DiscoveryResult(new Uri(path), Path.GetFileNameWithoutExtension(path), orderedInfos);
        result.FailedUrls.AddRange(orderedFailures);
        return result;

        async Task DiscoverBatchEntryAsync(int index, BatchFileEntry entry)
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                entryInfos[index] = await GetForUrlAsync(entry.Url, credential, entry.SuggestedSaveFolder, entry.SuggestedFilename, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error) when (error is YtdlpException or JsonException or InvalidOperationException or ArgumentException)
            {
                failedUrls[index] = entry.Url.ToString();
                _logger.LogWarning(error, "Unable to discover batch entry {Url}", entry.Url);
            }
            finally
            {
                semaphore.Release();
            }
        }
    }

    public Task<DiscoveryResult> GetForUrlAsync(Uri url, Credential? credential = null, CancellationToken cancellationToken = default) => GetForUrlAsync(url, credential, string.Empty, string.Empty, cancellationToken);

    private async Task<DiscoveryResult> GetForUrlAsync(Uri url, Credential? credential, string suggestedSaveFolder, string suggestedFilename, CancellationToken cancellationToken = default, HashSet<Uri>? ancestors = null)
    {
        ancestors ??= new HashSet<Uri>();
        if (ancestors.Count >= 16 || !ancestors.Add(url))
        {
            throw new YtdlpException("The playlist contains a cycle or exceeds the nesting limit.");
        }
        try
        {
            _logger.LogInformation($"Discovering media for {url}...");
            var processResult = await _ytdlpExecutableService.ExecuteAsync(_ytdlpExecutableService.GetDiscoveryProcessArguments(url, credential), cancellationToken);
            var jsonStartIndex = processResult.Output?.IndexOf('{') ?? -1;
            if (processResult.ExitCode != 0 && jsonStartIndex == -1)
            {
                _logger.LogError($"Failed to discover media for {url}: {processResult.Error.TrimEnd()}");
                throw new YtdlpException(processResult.Error);
            }
            if (jsonStartIndex == -1)
            {
                _logger.LogError($"Empty or non-JSON output from yt-dlp for {url}: {processResult.Error.TrimEnd()}");
                throw new YtdlpException(!string.IsNullOrWhiteSpace(processResult.Error) ? processResult.Error : processResult.Output ?? string.Empty);
            }
            cancellationToken.ThrowIfCancellationRequested();
            var jsonPayload = jsonStartIndex > 0 ? processResult.Output![jsonStartIndex..] : processResult.Output!;
            using var json = ParseDiscoveryJson(jsonPayload);
            if (json.RootElement.ValueKind != JsonValueKind.Object)
            {
                _logger.LogError($"Unexpected output format from yt-dlp for {url}: {processResult.Output.TrimEnd()}");
                throw new YtdlpException($"Unexpected output format from yt-dlp: {processResult.Output}");
            }
            DiscoveryResult? result = null;
            if (json!.RootElement.TryGetProperty("entries", out var entriesProperty) && entriesProperty.ValueKind == JsonValueKind.Array && entriesProperty.GetArrayLength() > 0)
            {
                var tabUrls = new List<Uri>();
                foreach (var entry in entriesProperty.EnumerateArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (entry.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }
                    if (entry.TryGetProperty("ie_key", out var ieProperty) && ieProperty.ValueKind == JsonValueKind.String && ieProperty.GetString() == "YoutubeTab" && entry.TryGetProperty("url", out var urlProperty) && urlProperty.ValueKind == JsonValueKind.String)
                    {
                        tabUrls.Add(new Uri(urlProperty.GetString() ?? string.Empty));
                    }
                }
                if (tabUrls.Count > 0)
                {
                    var tabResults = new DiscoveryResult?[tabUrls.Count];
                    using var semaphore = new SemaphoreSlim(4, 4);
                    var tasks = new Task[tabUrls.Count];
                    for (var i = 0; i < tabUrls.Count; i++)
                    {
                        tasks[i] = DiscoverTabAsync(i, tabUrls[i], new HashSet<Uri>(ancestors));
                    }
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    var urlInfos = new List<DiscoveryResult>(tabUrls.Count);
                    for (var i = 0; i < tabResults.Length; i++)
                    {
                        if (tabResults[i] is { } urlInfo)
                        {
                            urlInfos.Add(urlInfo);
                        }
                    }
                    if (urlInfos.Count > 0)
                    {
                        var title = json.RootElement.TryGetProperty("title", out var titleProperty) && titleProperty.ValueKind == JsonValueKind.String
                            ? titleProperty.GetString() ?? "Tab" : "Tab";
                        result = new DiscoveryResult(url, title, urlInfos);
                    }

                    async Task DiscoverTabAsync(int index, Uri tabUrl, HashSet<Uri> branchAncestors)
                    {
                        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                        try
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            tabResults[index] = await GetForUrlAsync(tabUrl, credential, suggestedSaveFolder, suggestedFilename, cancellationToken, branchAncestors).ConfigureAwait(false);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }
                }
            }
            if (result is null)
            {
                result = new DiscoveryResult(json.RootElement, _configurationService, _translationService, url, suggestedSaveFolder, suggestedFilename);
            }
            foreach (var media in result.Media)
            {
                _thumbnailService.MapMedia(media);
            }
            _logger.LogInformation($"Discovered media for {url}: {result.Media.Count} item(s)");
            return result;
        }
        finally
        {
            ancestors.Remove(url);
        }
    }

    private static JsonDocument ParseDiscoveryJson(string payload)
    {
        try
        {
            return JsonDocument.Parse(payload);
        }
        catch (JsonException)
        {
            throw new YtdlpException("The extractor returned invalid media information.");
        }
    }

    private async Task<List<BatchFileEntry>> ParseBatchFileAsync(string batchFilePath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation($"Parsing batch file: {batchFilePath}");
        if (!File.Exists(batchFilePath) || !Path.GetExtension(batchFilePath).Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }
        var result = new List<BatchFileEntry>();
        await foreach (var line in File.ReadLinesAsync(batchFilePath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var firstDelimiterIndex = line.IndexOf(BatchFileDelimiter);
            var secondDelimiterIndex = firstDelimiterIndex == -1 ? -1 : line.IndexOf(BatchFileDelimiter, firstDelimiterIndex + 1);
            var thirdDelimiterIndex = secondDelimiterIndex == -1 ? -1 : line.IndexOf(BatchFileDelimiter, secondDelimiterIndex + 1);
            if (thirdDelimiterIndex != -1)
            {
                continue;
            }
            var urlField = (firstDelimiterIndex == -1 ? line : line[..firstDelimiterIndex]).Clean();
            if (!urlField.TryExtractHttpUri(out var url) && !Uri.TryCreate(urlField, UriKind.Absolute, out url!))
            {
                continue;
            }
            var entry = new BatchFileEntry(url);
            if (firstDelimiterIndex != -1)
            {
                var saveFolder = (secondDelimiterIndex == -1 ? line[(firstDelimiterIndex + 1)..] : line[(firstDelimiterIndex + 1)..secondDelimiterIndex]).Clean();
                if (saveFolder.StartsWith("~", StringComparison.Ordinal))
                {
                    saveFolder = UserDirectories.Home + saveFolder[1..];
                }
                if (Path.IsPathRooted(saveFolder))
                {
                    entry.SuggestedSaveFolder = saveFolder;
                }
            }
            if (secondDelimiterIndex != -1)
            {
                var filename = line[(secondDelimiterIndex + 1)..].Clean();
                if (!string.IsNullOrEmpty(filename))
                {
                    entry.SuggestedFilename = filename;
                }
            }
            _logger.LogInformation($"Found batch file entry {entry.Url} with path: {Path.Combine(entry.SuggestedSaveFolder, entry.SuggestedFilename)}");
            result.Add(entry);
        }
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation($"Parsed {result.Count} entries in batch file: {batchFilePath}");
        return result;
    }
}
