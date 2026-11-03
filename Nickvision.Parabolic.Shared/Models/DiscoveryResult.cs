using Nickvision.Desktop.Application;
using Nickvision.Desktop.Globalization;
using Nickvision.Desktop.Helpers;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;

namespace Nickvision.Parabolic.Shared.Models;

public class DiscoveryResult
{
    private static int _nextId = 0;

    public int Id { get; }
    public Uri Url { get; }
    public string Title { get; }
    public bool HasSuggestedSaveFolder { get; }
    public List<Media> Media { get; }
    public List<string> FailedUrls { get; } = new();

    public bool IsPlaylist => Media.Count > 1;

    static DiscoveryResult()
    {
        _nextId = 0;
    }

    private DiscoveryResult(Uri url, string suggestedSaveFolder, string suggestedSaveFilename)
    {
        Id = Interlocked.Increment(ref _nextId);
        Url = url;
        Title = suggestedSaveFilename;
        HasSuggestedSaveFolder = !string.IsNullOrEmpty(suggestedSaveFolder);
        Media = new List<Media>();
    }

    public DiscoveryResult(JsonElement ytdlp, IConfigurationService configurationService, ITranslationService translator, Uri url, string suggestedSaveFolder, string suggestedSaveFilename) : this(url, suggestedSaveFolder, suggestedSaveFilename)
    {
        if (ytdlp.TryGetProperty("title", out var titleProperty) && titleProperty.ValueKind == JsonValueKind.String)
        {
            Title = titleProperty.GetString() ?? string.Empty;
        }
        if (ytdlp.TryGetProperty("entries", out var entriesProperty))
        {
            if (entriesProperty.ValueKind != JsonValueKind.Array)
            {
                throw new YtdlpException("The extractor returned an invalid playlist.");
            }
            AddEntries(entriesProperty, configurationService, translator, suggestedSaveFolder, Url);
            var urlCounts = new Dictionary<string, int>(Media.Count);
            foreach (var m in Media)
            {
                if (m.PlaylistPosition != -1 && !m.Url.IsEmpty)
                {
                    var key = m.Url.ToString();
                    urlCounts[key] = urlCounts.GetValueOrDefault(key, 0) + 1;
                }
            }
            foreach (var m in Media)
            {
                if (m.PlaylistPosition != -1 && !m.Url.IsEmpty && urlCounts.GetValueOrDefault(m.Url.ToString(), 0) > 1)
                {
                    m.RequiresPlaylistItems = true;
                }
            }
        }
        else
        {
            Media.Add(new Media(ytdlp, configurationService, translator, suggestedSaveFolder, suggestedSaveFilename, Url));
        }
    }

    private void AddEntries(JsonElement entriesProperty, IConfigurationService configurationService, ITranslationService translator, string suggestedSaveFolder, Uri? parentUrl, int depth = 0)
    {
        if (depth >= 16)
        {
            throw new YtdlpException("The playlist exceeds the nesting limit.");
        }
        var position = 0;
        foreach (var mediaObject in entriesProperty.EnumerateArray())
        {
            position++;
            if (mediaObject.ValueKind != JsonValueKind.Object)
            {
                continue;
            }
            if (mediaObject.TryGetProperty("entries", out var nestedEntries))
            {
                if (nestedEntries.ValueKind != JsonValueKind.Array)
                {
                    throw new YtdlpException("The extractor returned an invalid playlist.");
                }
                if (nestedEntries.GetArrayLength() == 0)
                {
                    continue;
                }
                Uri? nestedUrl = null;
                foreach (var property in new[] { "webpage_url", "original_url", "url" })
                {
                    if (mediaObject.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                        && Uri.TryCreate(value.GetString(), UriKind.Absolute, out var candidate)
                        && (candidate.Scheme == Uri.UriSchemeHttps || candidate.Scheme == Uri.UriSchemeHttp)
                        && !Models.Media.IsDirectMediaStreamUrl(candidate.ToString()))
                    {
                        nestedUrl = candidate;
                        break;
                    }
                }
                AddEntries(nestedEntries, configurationService, translator, suggestedSaveFolder, nestedUrl, depth + 1);
                continue;
            }
            var media = new Media(mediaObject, configurationService, translator, suggestedSaveFolder, string.Empty, parentUrl, position);
            if (!media.Url.IsEmpty)
            {
                Media.Add(media);
            }
            else
            {
                throw new InvalidOperationException("A playlist entry has no independently addressable URL.");
            }
        }
    }

    public DiscoveryResult(Uri url, string title, List<DiscoveryResult> urlInfos) : this(url, string.Empty, title)
    {
        Title = title;
        foreach (var urlInfo in urlInfos)
        {
            FailedUrls.AddRange(urlInfo.FailedUrls);
            HasSuggestedSaveFolder |= urlInfo.HasSuggestedSaveFolder;
            Media.EnsureCapacity(Media.Count + urlInfo.Media.Count);
            foreach (var media in urlInfo.Media)
            {
                Media.Add(media);
            }
        }
    }
}
