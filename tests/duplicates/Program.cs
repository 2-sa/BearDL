using Nickvision.Parabolic.Shared.Helpers;
using Nickvision.Parabolic.Shared.Models;

var first = new Uri("https://example.com/watch?v=first");
var second = new Uri("https://example.com/watch?v=second");
var active = new HashSet<Uri>();
var saved = new Dictionary<Uri, string>();
var file = Path.GetTempFileName();
try
{
    Check(DuplicateDownloadDetector.Find(new[] { first }, active, saved).Count == 0, "A new URL is allowed");
    active.Add(first);
    var result = DuplicateDownloadDetector.Find(new[] { first }, active, saved);
    Check(result.Count == 1 && result[0].Reason == DuplicateDownloadReason.Active, "An active or queued URL warns");
    saved[first] = file;
    result = DuplicateDownloadDetector.Find(new[] { first }, active, saved);
    Check(result[0].Reason == DuplicateDownloadReason.Active, "Active downloads take precedence over an old file");
    active.Clear();
    result = DuplicateDownloadDetector.Find(new[] { first }, active, saved);
    Check(result.Count == 1 && result[0].Reason == DuplicateDownloadReason.Saved && result[0].Path == file, "An existing saved file warns");
    File.Delete(file);
    Check(DuplicateDownloadDetector.Find(new[] { first }, active, saved).Count == 0, "A deleted file does not warn");
    saved.Clear();
    result = DuplicateDownloadDetector.Find(new[] { first, first, first }, active, saved);
    Check(result.Count == 1 && result[0].Reason == DuplicateDownloadReason.RepeatedInRequest, "Repeated batch entries produce one warning");
    Check(DuplicateDownloadDetector.Find(new[] { first, second }, active, saved).Count == 0, "Different videos on the same site are allowed");
    active.Add(first);
    active.Add(second);
    result = DuplicateDownloadDetector.Find(new[] { first, second, first }, active, saved);
    Check(result.Count == 2 && result.All(item => item.Reason == DuplicateDownloadReason.Active), "Batch checks include each conflicting URL once");
    Check(DuplicateDownloadDetector.Find(Array.Empty<Uri>(), active, saved).Count == 0, "Empty selections are allowed");
    Check("https://www.tiktok.com/@user/video/7683926991468367121".TryExtractHttpUri(out var tiktokUri) && tiktokUri.Host == "www.tiktok.com", "Direct HTTPS URL is extracted");
    Check("شاهد هذا المقطع الرائع https://vt.tiktok.com/ZSh12345/ من تيك توك".TryExtractHttpUri(out var sharedUri) && sharedUri.ToString() == "https://vt.tiktok.com/ZSh12345/", "Social share text extracts embedded URL");
    Check("\u2066https://www.instagram.com/reel/C9abc123/\u2069".TryExtractHttpUri(out var bidiUri) && bidiUri.Host == "www.instagram.com", "Bidi-isolated URL is extracted cleanly");
    Check(!("//example.com/path".TryExtractHttpUri(out _)), "Protocol-relative string is rejected");
    Console.WriteLine("Duplicate download checks passed (13 scenarios).");
    BearDL.Tests.ResourceChecks.Run();
}
finally
{
    File.Delete(file);
}

static void Check(bool condition, string scenario)
{
    if (!condition)
    {
        throw new InvalidOperationException(scenario);
    }
}
