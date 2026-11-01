using Nickvision.Desktop.Application;
using Nickvision.Desktop.Globalization;
using Nickvision.Desktop.Helpers;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Nickvision.Parabolic.Shared.Models;

public class DiscoveryResult
{
    private static int _nextId = 0;

    public int Id { get; }
    public Uri Url { get; }
    public string Title { get; }
    public bool HasSuggestedSaveFolder { get; }
    public List<Media> Media { get; }

    public bool IsPlaylist => Media.Count > 1;

    static DiscoveryResult()
    {
        _nextId = 0;
    }

    private DiscoveryResult(Uri url, string suggestedSaveFolder, string suggestedSaveFilename)
    {
        Id = _nextId++;
        Url = url;
        Title = suggestedSaveFilename;
        HasSuggestedSaveFolder = !string.IsNullOrEmpty(suggestedSaveFolder);
        Media = new List<Media>();
    }

    public DiscoveryResult(JsonElement ytdlp, IConfigurationService configurationService, ITranslationService translator, Uri url, string suggestedSaveFolder, string suggestedSaveFilename) : this(url, suggestedSaveFolder, suggestedSaveFilename)
    {
        if (ytdlp.TryGetProperty("title", out var titleProperty) && titleProperty.ValueKind != JsonValueKind.Null)
        {
            Title = titleProperty.GetString() ?? string.Empty;
        }
        if (ytdlp.TryGetProperty("entries", out var entriesProperty) && entriesProperty.ValueKind == JsonValueKind.Array && entriesProperty.GetArrayLength() > 0)
        {
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

    private void AddEntries(JsonElement entriesProperty, IConfigurationService configurationService, ITranslationService translator, string suggestedSaveFolder, Uri? parentUrl)
    {
        var position = 0;
        foreach (var mediaObject in entriesProperty.EnumerateArray())
        {
            position++;
            if (mediaObject.ValueKind != JsonValueKind.Object)
            {
                continue;
            }
            if (mediaObject.TryGetProperty("entries", out var nestedEntries) && nestedEntries.ValueKind == JsonValueKind.Array && nestedEntries.GetArrayLength() > 0)
            {
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
                AddEntries(nestedEntries, configurationService, translator, suggestedSaveFolder, nestedUrl);
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
            HasSuggestedSaveFolder |= urlInfo.HasSuggestedSaveFolder;
            Media.EnsureCapacity(Media.Count + urlInfo.Media.Count);
            foreach (var media in urlInfo.Media)
            {
                Media.Add(media);
            }
        }
    }
}
