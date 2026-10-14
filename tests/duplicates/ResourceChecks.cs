using Nickvision.Parabolic.Shared.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace BearDL.Tests;

public static class ResourceChecks
{
    public static void Run()
    {
        var url = new Uri("https://example.com/watch?v=resource-check");
        var repeated = new Uri[10000];
        Array.Fill(repeated, url);
        var active = new HashSet<Uri>();
        var saved = new Dictionary<Uri, string> { [url] = "saved-file" };
        var fileChecks = 0;
        Func<string, bool> exists = _ => { fileChecks++; return true; };
        // Warm up outside the measurement; inputs and callback are already built.
        DuplicateDownloadDetector.Find(repeated, active, saved, exists);
        fileChecks = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();
        var result = DuplicateDownloadDetector.Find(repeated, active, saved, exists);
        var elapsed = Stopwatch.GetElapsedTime(started);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        if (result.Count != 1 || fileChecks != 1)
        {
            throw new InvalidOperationException("A repeated saved URL must produce one warning and one disk check.");
        }
        if (allocated > 65536)
        {
            throw new InvalidOperationException($"Repeated URL checks allocated {allocated} bytes; allocations must not grow with repeated entries.");
        }
        active.Add(url);
        fileChecks = 0;
        result = DuplicateDownloadDetector.Find(repeated, active, saved, exists);
        if (result.Count != 1 || fileChecks != 0)
        {
            throw new InvalidOperationException("Active URLs must not check saved files.");
        }
        Console.WriteLine($"Resource checks passed: 10,000 repeated entries, {allocated} allocated bytes, {elapsed.TotalMilliseconds:F3} ms, one disk check; active URLs use zero disk checks.");
    }
}
