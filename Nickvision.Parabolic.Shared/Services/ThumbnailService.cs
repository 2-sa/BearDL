using Microsoft.Extensions.Logging;
using Nickvision.Desktop.Helpers;
using Nickvision.Parabolic.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nickvision.Parabolic.Shared.Services;

public class ThumbnailService : IThumbnailService
{
    private const int MaxCacheEntries = 96;

    private readonly ILogger<ThumbnailService> _logger;
    private readonly HttpClient _httpClient;
    private readonly object _sync;
    private readonly byte[] _defaultThumbnail;
    private readonly Dictionary<Uri, (byte[] Bytes, LinkedListNode<Uri>? Node)> _cache;
    private readonly LinkedList<Uri> _lruOrder;
    private readonly Dictionary<Uri, Uri> _mediaMap;
    private readonly Dictionary<Uri, Task<byte[]>> _inFlight;

    public ThumbnailService(ILogger<ThumbnailService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _sync = new object();
        _cache = [];
        _lruOrder = [];
        _mediaMap = [];
        _inFlight = [];
        _logger.LogDebug("Loading default thumbnail into cache...");
        using var stream = typeof(ThumbnailService).Assembly.GetManifestResourceStream("Nickvision.Parabolic.Shared.Resources.default_thumbnail.jpg");
        if (stream is null)
        {
            _logger.LogWarning("Default thumbnail resource not found. Using empty byte array.");
            _defaultThumbnail = [];
            _cache[Uri.Empty] = (_defaultThumbnail, null);
            return;
        }
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        _defaultThumbnail = memoryStream.ToArray();
        _cache[Uri.Empty] = (_defaultThumbnail, null);
        _logger.LogDebug("Loaded default thumbnail into cache.");
    }

    public Task<byte[]> GetImageBytesAsync(Uri url)
    {
        _logger.LogDebug($"Getting image bytes for url: {url}");
        return GetOrDownloadImageBytesAsync(url, "bytes");
    }

    public async Task<MemoryStream> GetImageStreamAsync(Uri url)
    {
        _logger.LogDebug($"Getting image stream for url: {url}");
        var bytes = await GetOrDownloadImageBytesAsync(url, "stream").ConfigureAwait(false);
        return new MemoryStream(bytes);
    }

    public void MapMedia(Media media)
    {
        lock (_sync)
        {
            if (_cache.ContainsKey(media.Url))
            {
                return;
            }
            if (media.ThumbnailUrl.IsEmpty)
            {
                SetCachedBytesLocked(media.Url, _defaultThumbnail);
                _logger.LogWarning($"Mapped media url ({media.Url}) to default thumbnail as thumbnail url is empty.");
            }
            else
            {
                _mediaMap[media.Url] = media.ThumbnailUrl;
                _logger.LogDebug($"Mapped media url ({media.Url}) to thumbnail url ({media.ThumbnailUrl}).");
            }
        }
    }

    private async Task<byte[]> GetOrDownloadImageBytesAsync(Uri url, string kind)
    {
        Uri fetchUrl;
        Task<byte[]> downloadTask;
        TaskCompletionSource<byte[]>? ownerTcs = null;
        lock (_sync)
        {
            if (TryGetCachedBytesLocked(url, out var cachedBytes))
            {
                _logger.LogDebug($"Found image {kind} for url ({url}) in cache.");
                return cachedBytes;
            }
            fetchUrl = _mediaMap.TryGetValue(url, out var mappedUrl) ? mappedUrl : url;
            if (fetchUrl == Uri.Empty)
            {
                SetCachedBytesLocked(url, _defaultThumbnail);
                _mediaMap.Remove(url);
                return _defaultThumbnail;
            }
            if (fetchUrl != url && TryGetCachedBytesLocked(fetchUrl, out var mappedCachedBytes))
            {
                SetCachedBytesLocked(url, mappedCachedBytes);
                _mediaMap.Remove(url);
                _logger.LogDebug($"Found image {kind} for mapped url ({fetchUrl}) in cache.");
                return mappedCachedBytes;
            }
            if (!_inFlight.TryGetValue(fetchUrl, out downloadTask!))
            {
                ownerTcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                downloadTask = ownerTcs.Task;
                _inFlight[fetchUrl] = downloadTask;
            }
        }
        if (ownerTcs is not null)
        {
            var result = _defaultThumbnail;
            try
            {
                _logger.LogDebug($"Downloading image bytes for url: {fetchUrl}");
                var downloaded = Array.Empty<byte>();
                try
                {
                    downloaded = await _httpClient.GetByteArrayAsync(fetchUrl).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    _logger.LogWarning($"Failed to download thumbnail for url ({fetchUrl}): {e.Message}. Using default thumbnail.");
                }
                if (downloaded.Length == 0)
                {
                    _logger.LogWarning($"Downloaded image bytes for url ({fetchUrl}) are empty. Using default thumbnail.");
                }
                else
                {
                    _logger.LogDebug($"Downloaded image bytes for url ({fetchUrl}).");
                    result = downloaded;
                }
            }
            catch (Exception e)
            {
                _logger.LogWarning($"Unexpected error downloading thumbnail for url ({fetchUrl}): {e.Message}. Using default thumbnail.");
                result = _defaultThumbnail;
            }
            finally
            {
                lock (_sync)
                {
                    SetCachedBytesLocked(fetchUrl, result);
                    _inFlight.Remove(fetchUrl);
                }
                ownerTcs.SetResult(result);
            }
        }
        var bytes = await downloadTask.ConfigureAwait(false);
        lock (_sync)
        {
            if (_mediaMap.Remove(url, out var removedUrl))
            {
                SetCachedBytesLocked(url, bytes);
                if (removedUrl != fetchUrl)
                {
                    SetCachedBytesLocked(removedUrl, bytes);
                }
                _logger.LogDebug($"Removed media url ({url}) from media map.");
            }
            else if (url != fetchUrl)
            {
                SetCachedBytesLocked(url, bytes);
            }
        }
        return bytes;
    }

    private bool TryGetCachedBytesLocked(Uri key, out byte[] bytes)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.Node is not null)
            {
                _lruOrder.Remove(entry.Node);
                _lruOrder.AddFirst(entry.Node);
            }
            bytes = entry.Bytes;
            return true;
        }
        bytes = [];
        return false;
    }

    private void SetCachedBytesLocked(Uri key, byte[] bytes)
    {
        if (key == Uri.Empty)
        {
            _cache[Uri.Empty] = (bytes, null);
            return;
        }
        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.Node is not null)
            {
                _lruOrder.Remove(entry.Node);
                _lruOrder.AddFirst(entry.Node);
            }
            _cache[key] = (bytes, entry.Node);
            return;
        }
        while (_cache.Count >= MaxCacheEntries && _lruOrder.Last is { } oldest)
        {
            _lruOrder.RemoveLast();
            _cache.Remove(oldest.Value);
        }
        var node = _lruOrder.AddFirst(key);
        _cache[key] = (bytes, node);
    }
}
