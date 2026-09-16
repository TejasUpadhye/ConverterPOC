**File Conversion PoC**

Proof of concept for converting large volumes of files through a third-party web application that has no API, cannot process files in parallel within a session, and may change without notice.

The problem
No API — the only interface is the web UI
~10 seconds per conversion
One file at a time per session
The application can change in any way at any time
Must handle thousands of files per 8-hour working day

The throughput ceiling follows directly: 28,800 seconds ÷ 10s = 2,880 files from a single serial pipeline. Anything beyond that requires multiple independent sessions, which is what this PoC demonstrates.

What this demonstrates
Browser automation with Playwright for .NET — driving a UI-only application
Real completion signals — waits on DOM state, never a fixed Thread.Sleep
Page object pattern — every selector confined to ConverterPage.cs, so a UI change is a one-file patch
Output validation — magic-byte and size checks, because a file that downloads successfully can still be wrong
Retry with exponential backoff, screenshot captured on final failure
Parallel workers, each with an isolated browser context
Measured throughput against the theoretical figure
Measured results
Metric	Value
Files processed	20
Workers	3
Wall clock	(fill in)
Average per file	(fill in)
Measured rate	(fill in) files/hour
Projected 8h day	(fill in) files

The measured per-file time exceeds the stated 10 seconds, because that figure covers only the provider's processing. Page load, upload, download and disk write add meaningful overhead. Sizing capacity from the theoretical number alone would leave the pipeline short.

What this deliberately does not do

Runs against a local stand-in, not a live converter. The stand-in simulates the stated constraints — UI-only, ~10 second conversion, one file at a time. Pointing automation at a real converter would breach its terms of service, which is the first risk identified in the written design. Demonstrating a solution by committing the risk it warns about would be self-defeating.

No queue, containers or orchestration. These are described in the written design. They are standard infrastructure rather than the interesting part, and building them here would prove nothing the design document does not already state. The in-memory ConcurrentQueue stands in for a durable broker.

Does not test real-world hostility. CAPTCHAs, session expiry, rate limiting and IP blocking cannot be reproduced against a local stand-in. Those would need a spike against the actual target, with terms-of-service clearance first.

Project structure
ConverterPoc/
├── Pages/
│   └── ConverterPage.cs        All UI selectors live here, and nowhere else
├── Workers/
│   └── ConversionWorker.cs     One worker: claim, convert, validate, retry
├── Validation/
│   └── OutputValidator.cs      Magic-byte and size checks on downloads
└── Program.cs                  Queue setup, worker orchestration, reporting
Running it

Prerequisites: .NET 10 SDK, and the Playwright browser binaries.

powershell
# one-time setup
dotnet restore
dotnet build
pwsh bin\Debug\net10.0\playwright.ps1 install chromium

Serve the stand-in converter in one terminal:

powershell
dotnet tool install -g dotnet-serve
cd <folder containing fake-converter.html>
dotnet-serve --port 8000

Run the workers in another:

powershell
cd ConverterPoc
dotnet run

Sample input files are generated automatically on first run. Converted output and any failure screenshots are written to bin\Debug\net10.0\output\.

Set Headless = true in Program.cs for throughput runs; leave it false to watch the workers drive the browser.

Design notes

Why Playwright over Selenium. Auto-waiting rather than fixed sleeps — the 10 seconds is stated as approximate, so the variability matters. Isolated browser contexts allow multiple workers in one browser process. Native download handling via WaitForDownloadAsync. Trace viewer records DOM snapshots and network activity, which matters when debugging a failure caused by a site change that cannot be reproduced locally.

Why output validation gets the most attention. Every other failure mode is loud — crashes, timeouts, ban pages, queue backlogs. Silent corruption is not. Without validation, the first person to notice is a customer, by which point thousands of bad files have already been delivered.

Why selectors are isolated. The problem statement says the application may change at any time. That makes UI changes a certainty, not a risk. Confining every selector to one class turns an unpredictable event into a one-line fix.
