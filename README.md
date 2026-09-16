# File Conversion PoC

A proof of concept for converting large volumes of files through a third-party web application that:

* Has no API — the only available interface is the web UI.
* Takes approximately **10 seconds per conversion**.
* Allows only **one conversion at a time per browser session**.
* May change its UI or behavior without notice.
* Needs to process **thousands of files during an 8-hour working day**.

---

## 1. Problem Statement

The provider's processing time creates a theoretical throughput ceiling:

**8 hours = 28,800 seconds**

**28,800 ÷ 10 seconds = 2,880 files**

Therefore, a single serial pipeline can theoretically process a maximum of approximately **2,880 files per 8-hour day**.

To process more files, the system needs multiple independent sessions running in parallel.

This PoC demonstrates that approach using **Playwright for .NET**, with each worker operating in an isolated browser context.

---

## 2. What This PoC Demonstrates

### Browser Automation

Uses **Playwright for .NET** to drive a UI-only application where no API is available.

### Real Completion Signals

The automation waits for actual DOM state changes rather than relying on fixed `Thread.Sleep` delays.

This is important because the stated 10-second processing time is approximate and can vary.

### Page Object Pattern

All UI selectors are isolated inside `ConverterPage.cs`.

This means that if the third-party application's UI changes, selectors can be updated in one place without modifying the worker logic.

### Output Validation

Every downloaded file is validated using:

* File size checks
* Magic-byte / file-signature checks

A successful browser download does not necessarily mean that the resulting file is valid.

### Retry with Exponential Backoff

Failed conversions are retried using exponential backoff.

If the final retry fails, a screenshot is captured for troubleshooting.

### Parallel Workers

Multiple workers run simultaneously, with each worker using an isolated browser context.

This allows several independent sessions to process files concurrently.

### Throughput Measurement

The PoC measures actual processing performance and compares it against the theoretical throughput limit.

---

## 3. Measured Results

| Metric                      |                Value |
| --------------------------- | -------------------: |
| Files processed             |                   20 |
| Workers                     |                    3 |
| Wall-clock time             |            *Fill in* |
| Average time per file       |            *Fill in* |
| Measured rate               | *Fill in* files/hour |
| Projected 8-hour throughput |      *Fill in* files |

### Important Observation

The measured per-file processing time is expected to be higher than the stated **10 seconds**.

The 10-second figure represents only the provider's conversion time. The complete workflow also includes:

* Page loading
* File upload
* Provider-side processing
* Download
* Disk I/O
* Browser automation overhead

Therefore, sizing the system using the theoretical **2,880 files/day** figure alone would overestimate the actual capacity.

The measured end-to-end throughput should be used for capacity planning.

---

## 4. What This PoC Deliberately Does Not Do

### 4.1 Does Not Run Against a Live Converter

The PoC runs against a **local stand-in converter** rather than a real third-party service.

The stand-in simulates the stated constraints:

* UI-only interaction
* Approximately 10-second conversion time
* One conversion at a time per session

Running automation against a real third-party converter without appropriate authorization could violate its terms of service.

The written design therefore identifies this as a risk rather than demonstrating the solution by introducing that risk.

---

### 4.2 No Queue, Containers, or Orchestration

The production design describes the use of:

* A durable queue
* Containers
* Worker orchestration
* Horizontal scaling

These infrastructure components are intentionally not implemented in this PoC.

The in-memory `ConcurrentQueue` is used as a stand-in for a durable message broker.

The purpose of this PoC is to validate the core technical challenge: **reliable browser-based conversion with parallel isolated sessions**.

---

### 4.3 Does Not Test Real-World Hostility

The local stand-in cannot realistically reproduce issues such as:

* CAPTCHA challenges
* Session expiration
* Rate limiting
* IP blocking
* Provider-side throttling
* Unexpected UI changes
* Anti-bot mechanisms

Testing these behaviors would require a controlled spike against the actual target application, with appropriate authorization and terms-of-service clearance.

---

# 5. Project Structure

```text
ConverterPoc/
│
├── Pages/
│   └── ConverterPage.cs
│       └── All UI selectors and page interactions
│
├── Workers/
│   └── ConversionWorker.cs
│       └── Claim → Convert → Validate → Retry
│
├── Validation/
│   └── OutputValidator.cs
│       └── Magic-byte and file-size validation
│
└── Program.cs
    └── Queue setup, worker orchestration, and reporting
```

The main design principle is that **UI-specific logic stays inside `ConverterPage.cs`**.

---

# 6. Running the PoC

## Prerequisites

* **.NET 10 SDK**
* **Playwright**
* **Chromium browser binaries**

---

## 6.1 One-Time Setup

Restore dependencies:

```powershell
dotnet restore
```

Build the project:

```powershell
dotnet build
```

Install the Playwright Chromium browser:

```powershell
pwsh bin\Debug\net10.0\playwright.ps1 install chromium
```

---

## 6.2 Start the Stand-In Converter

Install `dotnet-serve` if it is not already installed:

```powershell
dotnet tool install -g dotnet-serve
```

Navigate to the folder containing `fake-converter.html`:

```powershell
cd <folder-containing-fake-converter.html>
```

Start the local web server:

```powershell
dotnet-serve --port 8000
```

The stand-in converter will then be available locally.

---

## 6.3 Run the Conversion Workers

Open another terminal and navigate to the project:

```powershell
cd ConverterPoc
```

Run the PoC:

```powershell
dotnet run
```

Sample input files are generated automatically during the first run.

Converted files and failure screenshots are written to:

```text
bin\Debug\net10.0\output\
```

---

## 6.4 Headless Mode

For throughput measurements, set:

```csharp
Headless = true
```

in `Program.cs`.

For development and debugging, leave it as:

```csharp
Headless = false
```

This allows you to visually observe the workers driving the browser.

---

# 7. Design Notes

## 7.1 Why Playwright Instead of Selenium?

Playwright was selected primarily because of its built-in waiting and browser automation capabilities.

The automation waits for actual page conditions rather than relying on fixed delays.

For example, instead of:

```csharp
Thread.Sleep(10000);
```

the worker waits for the relevant DOM state or download event.

This matters because the provider's **10-second conversion time is approximate**, meaning a fixed delay can either:

* Wait unnecessarily when processing finishes early, or
* Continue too early when processing takes longer.

Playwright also provides:

* Isolated browser contexts
* Native download handling
* Automatic waiting
* Tracing capabilities
* DOM snapshots
* Network activity capture

These features make it suitable for a UI-only automation workload.

---

## 7.2 Why Use Isolated Browser Contexts?

Each worker gets its own browser context.

Conceptually:

```text
Browser
│
├── Worker 1
│   └── Browser Context 1
│
├── Worker 2
│   └── Browser Context 2
│
└── Worker 3
    └── Browser Context 3
```

This allows multiple independent sessions to operate concurrently without sharing cookies, session state, or page state.

The approach increases throughput while respecting the provider's constraint of one conversion at a time per session.

---

## 7.3 Why Output Validation Gets the Most Attention

Output validation is one of the most important parts of the PoC.

Most failures are obvious:

* Application crashes
* Timeouts
* Failed uploads
* Failed downloads
* Session errors
* Queue backlogs

Invalid output can be much more dangerous because the conversion may appear successful.

Without validation, corrupted or incorrect files could silently enter downstream processing and potentially reach customers.

The PoC therefore validates the downloaded output using:

1. **File existence**
2. **File size**
3. **Magic bytes / file signature**

This provides a basic sanity check that the downloaded file is actually the expected file type.

---

## 7.4 Why Are Selectors Isolated?

The third-party application's UI may change without notice.

That makes UI changes an expected operational possibility rather than an exceptional event.

All selectors are therefore contained inside:

```text
Pages/ConverterPage.cs
```

Instead of spreading selectors throughout the application:

```text
Worker
 ├── selector
 ├── selector
 ├── selector
 └── selector
```

the worker interacts with a page abstraction:

```text
ConversionWorker
       │
       ▼
ConverterPage
       │
       ├── Upload
       ├── Wait for completion
       ├── Download
       └── Page selectors
```

If the provider changes a button, input field, or completion indicator, the change can generally be isolated to `ConverterPage.cs`.

This turns an unpredictable UI change into a localized maintenance task.

---

# 8. Overall Flow

```text
                 Input Files
                      │
                      ▼
              ┌───────────────┐
              │  Work Queue   │
              └───────┬───────┘
                      │
          ┌───────────┼───────────┐
          │           │           │
          ▼           ▼           ▼
      Worker 1    Worker 2    Worker 3
          │           │           │
          ▼           ▼           ▼
      Browser      Browser      Browser
      Context 1   Context 2   Context 3
          │           │           │
          ▼           ▼           ▼
       Upload      Upload      Upload
          │           │           │
          ▼           ▼           ▼
      Convert      Convert      Convert
          │           │           │
          ▼           ▼           ▼
       Download    Download    Download
          │           │           │
          ▼           ▼           ▼
       Validate    Validate    Validate
          │           │           │
          └───────────┼───────────┘
                      ▼
                Output Files
```

---

# 9. Key Takeaways

The PoC validates the core approach for scaling a UI-only file conversion workflow:

* **Parallelism is required** to exceed the throughput of a single serial session.
* **Browser contexts provide session isolation** for independent workers.
* **DOM-based completion signals are preferable to fixed sleeps.**
* **Output validation is essential** to detect silent conversion failures.
* **Page Object Pattern limits the impact of UI changes.**
* **Retries with exponential backoff** improve resilience to transient failures.
* **Actual end-to-end throughput must be measured** because provider processing time is only one component of total processing time.
* Production deployment would require additional infrastructure and controls, including a durable queue, worker scaling, observability, failure handling, and appropriate authorization to automate the target service.
