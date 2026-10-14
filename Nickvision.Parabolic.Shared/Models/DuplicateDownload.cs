using System;
using System.Collections.Generic;
using System.IO;

namespace Nickvision.Parabolic.Shared.Models;

public enum DuplicateDownloadReason
{
    Active,
    Saved,
    RepeatedInRequest
}

public sealed record DuplicateDownload(Uri Url, DuplicateDownloadReason Reason, string? Path = null);

public static class DuplicateDownloadDetector
{
    public static IReadOnlyList<DuplicateDownload> Find(IEnumerable<Uri> urls, IReadOnlySet<Uri> activeUrls, IReadOnlyDictionary<Uri, string> savedFiles, Func<string, bool>? fileExists = null)
    {
        var seen = new HashSet<Uri>();
        var duplicates = new Dictionary<Uri, DuplicateDownload>();
        fileExists ??= File.Exists;
        foreach (var url in urls)
        {
            if (!seen.Add(url))
            {
                if (!duplicates.ContainsKey(url))
                {
                    duplicates.Add(url, new DuplicateDownload(url, DuplicateDownloadReason.RepeatedInRequest));
                }
                continue;
            }
            if (activeUrls.Contains(url))
            {
                duplicates[url] = new DuplicateDownload(url, DuplicateDownloadReason.Active);
            }
            else if (savedFiles.TryGetValue(url, out var path) && fileExists(path))
            {
                duplicates[url] = new DuplicateDownload(url, DuplicateDownloadReason.Saved, path);
            }
        }
        return new List<DuplicateDownload>(duplicates.Values);
    }
}
