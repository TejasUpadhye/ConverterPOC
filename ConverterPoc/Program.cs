using System.Collections.Concurrent;
using System.Diagnostics;
using ConverterPoc.Workers;
using Microsoft.Playwright;

const int WorkerCount = 3;
const int SampleFiles = 20;

var inputDir = Path.Combine(AppContext.BaseDirectory, "input");
var outputDir = Path.Combine(AppContext.BaseDirectory, "output");
Directory.CreateDirectory(inputDir);
Directory.CreateDirectory(outputDir);

GenerateSampleFiles(inputDir, SampleFiles);

var queue = new ConcurrentQueue<string>(Directory.GetFiles(inputDir, "*.pdf"));
var results = new ConcurrentBag<JobResult>();

Console.WriteLine($"Queued {queue.Count} files across {WorkerCount} workers\n");

using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync(new()
{
    Headless = false,   // watch it work; set true for throughput runs
    SlowMo = 0
});

var wall = Stopwatch.StartNew();

var workers = Enumerable.Range(1, WorkerCount)
    .Select(i => new ConversionWorker(i, browser, queue, outputDir, results).RunAsync());

await Task.WhenAll(workers);

wall.Stop();
Report(results, wall.Elapsed, WorkerCount);


static void GenerateSampleFiles(string dir, int count)
{
    for (var i = 1; i <= count; i++)
    {
        var path = Path.Combine(dir, $"sample-{i:D3}.pdf");
        if (!File.Exists(path))
            File.WriteAllText(path, "%PDF-1.4\n" + new string('x', 500) + "\n%%EOF");
    }
}

static void Report(ConcurrentBag<JobResult> results, TimeSpan wall, int workers)
{
    var ok = results.Count(r => r.Success);
    var failed = results.Count(r => !r.Success);

    Console.WriteLine($"""

        ─────────── RESULTS ───────────
        Succeeded          : {ok}
        Failed             : {failed}
        Wall clock         : {wall.TotalSeconds:F1}s
        """);

    if (ok == 0)
    {
        Console.WriteLine("No successful conversions — throughput not measurable.");
        Console.WriteLine("───────────────────────────────");
        PrintFailures(results);
        return;
    }

    var avgMs = results.Where(r => r.Success).Average(r => r.ElapsedMs);

    // Measured from wall clock, not per-file average — includes all real overhead.
    var actualPerHour = ok / wall.TotalHours;
    var projectedDaily = actualPerHour * 8;

    Console.WriteLine($"""
        Avg per file       : {avgMs:F0}ms
        Measured rate      : {actualPerHour:F0} files/hour  ({workers} workers)
        Projected 8h day   : {projectedDaily:F0} files
        ───────────────────────────────
        """);

    PrintFailures(results);
}

static void PrintFailures(ConcurrentBag<JobResult> results)
{
    foreach (var f in results.Where(r => !r.Success))
        Console.WriteLine($"  FAILED: {Path.GetFileName(f.File)} — {f.Error}");
}