using System.Collections.Concurrent;
using System.Diagnostics;
using ConverterPoc.Pages;
using ConverterPoc.Validation;
using Microsoft.Playwright;

namespace ConverterPoc.Workers;

public record JobResult(string File, bool Success, long ElapsedMs, string? Error = null);

public class ConversionWorker
{
    private const int MaxAttempts = 3;
    private const int RecycleEvery = 25;

    private readonly int _id;
    private readonly IBrowser _browser;
    private readonly ConcurrentQueue<string> _queue;
    private readonly string _outputDir;
    private readonly ConcurrentBag<JobResult> _results;

    public ConversionWorker(int id, IBrowser browser, ConcurrentQueue<string> queue,
                            string outputDir, ConcurrentBag<JobResult> results)
    {
        _id = id; _browser = browser; _queue = queue;
        _outputDir = outputDir; _results = results;
    }

    public async Task RunAsync()
    {
        // Isolated context per worker — separate cookies, storage, session.
        var context = await _browser.NewContextAsync(new() { AcceptDownloads = true });
        var page = await context.NewPageAsync();
        var converter = new ConverterPage(page);
        await converter.OpenAsync();

        var processed = 0;

        while (_queue.TryDequeue(out var inputFile))
        {
            var sw = Stopwatch.StartNew();
            var result = await ProcessWithRetryAsync(converter, inputFile);
            sw.Stop();

            result = result with { ElapsedMs = sw.ElapsedMilliseconds };
            _results.Add(result);

            Console.WriteLine(result.Success
                ? $"[W{_id}] OK   {Path.GetFileName(inputFile)}  {sw.ElapsedMilliseconds}ms"
                : $"[W{_id}] FAIL {Path.GetFileName(inputFile)}  {result.Error}");

            // Recycle the page periodically — avoids memory growth over long runs.
            if (++processed % RecycleEvery == 0)
            {
                await page.CloseAsync();
                page = await context.NewPageAsync();
                converter = new ConverterPage(page);
                await converter.OpenAsync();
            }
        }

        await context.CloseAsync();
    }

    private async Task<JobResult> ProcessWithRetryAsync(ConverterPage converter, string inputFile)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await converter.OpenAsync();   // fresh state for each job
                await converter.UploadAsync(inputFile);
                await converter.StartConversionAsync();
                await converter.WaitForCompletionAsync();

                var outPath = Path.Combine(_outputDir,
                    Path.GetFileNameWithoutExtension(inputFile) + "-converted.pdf");

                await converter.DownloadToAsync(outPath);

                var validation = OutputValidator.Validate(outPath);
                if (!validation.IsValid)
                {
                    File.Delete(outPath);   // never keep a corrupt output
                    throw new InvalidDataException(validation.Reason);
                }

                return new JobResult(inputFile, true, 0);
            }
            catch (Exception ex)
            {
                if (attempt == MaxAttempts)
                {
                    var shot = Path.Combine(_outputDir,
                        $"FAIL-{Path.GetFileNameWithoutExtension(inputFile)}.png");
                    try { await converter.CaptureFailureAsync(shot); } catch { }

                    return new JobResult(inputFile, false, 0, ex.Message);
                }

                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));  // backoff
            }
        }

        return new JobResult(inputFile, false, 0, "Unreachable");
    }
}