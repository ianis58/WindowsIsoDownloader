using Microsoft.Playwright;

namespace WindowsIsoDownloader.Diagnostics
{
    /// <summary>Saves a screenshot and the HTML of a page when a run fails.</summary>
    public static class DiagnosticsWriter
    {
        /// <summary>
        /// Captures the page state into <paramref name="directory"/>. Returns the
        /// path of the screenshot, or <c>null</c> when it could not be captured.
        /// </summary>
        public static async Task<string?> CaptureAsync(IPage page, string directory)
        {
            ArgumentNullException.ThrowIfNull(page);

            try
            {
                Directory.CreateDirectory(directory);
                var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                var screenshotPath = Path.Combine(directory, $"failure-{stamp}.png");
                var htmlPath = Path.Combine(directory, $"failure-{stamp}.html");

                await page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true }).ConfigureAwait(false);
                await File.WriteAllTextAsync(htmlPath, await page.ContentAsync().ConfigureAwait(false)).ConfigureAwait(false);

                return screenshotPath;
            }
            catch (Exception)
            {
                // Diagnostics must never mask the original error.
                return null;
            }
        }
    }
}
