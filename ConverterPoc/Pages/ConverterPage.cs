using Microsoft.Playwright;

namespace ConverterPoc.Pages;

/// <summary>
/// All knowledge of the third-party UI is confined to this class.
/// When the site changes, this is the only file that needs editing.
/// </summary>
public class ConverterPage
{
    private const string Url = "http://localhost:8000/fake-converter.html";
    private const string FileInput = "#file-upload";
    private const string ConvertButton = "#convert-btn";
    private const string DownloadLink = "#download-link";
    private const string StatusText = "#status";

    private readonly IPage _page;

    public ConverterPage(IPage page) => _page = page;

    public async Task OpenAsync() =>
        await _page.GotoAsync(Url, new() { WaitUntil = WaitUntilState.DOMContentLoaded });

    public async Task UploadAsync(string filePath) =>
        await _page.SetInputFilesAsync(FileInput, filePath);

    public async Task StartConversionAsync() =>
        await _page.ClickAsync(ConvertButton);

    /// <summary>
    /// Waits on the real completion signal, never a fixed sleep.
    /// Timeout is generous: their 10s is an average, not a guarantee.
    /// </summary>
    public async Task WaitForCompletionAsync(int timeoutMs = 60_000) =>
        await _page.WaitForSelectorAsync(DownloadLink, new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = timeoutMs
        });

    public async Task<string> DownloadToAsync(string destinationPath)
    {
        var waitForDownload = _page.WaitForDownloadAsync();
        await _page.ClickAsync(DownloadLink);
        var download = await waitForDownload;
        await download.SaveAsAsync(destinationPath);
        return destinationPath;
    }

    public async Task<string> GetStatusAsync() =>
        await _page.InnerTextAsync(StatusText);

    /// <summary>Captured on failure — the site may have changed.</summary>
    public async Task CaptureFailureAsync(string path) =>
        await _page.ScreenshotAsync(new() { Path = path, FullPage = true });
}