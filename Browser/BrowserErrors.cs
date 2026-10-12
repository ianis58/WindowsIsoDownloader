using Microsoft.Playwright;

namespace WindowsIsoDownloader.Browser
{
    /// <summary>
    /// Classifies the exceptions Playwright can throw while driving the browser.
    /// <para>
    /// Note: Playwright reports timeouts as <see cref="TimeoutException"/>, not as
    /// <see cref="PlaywrightException"/>, so both families must be considered.
    /// </para>
    /// </summary>
    public static class BrowserErrors
    {
        /// <summary>True when the error is a browser/driver error that can be retried or skipped.</summary>
        public static bool IsRecoverable(Exception exception)
        {
            return exception is PlaywrightException or TimeoutException;
        }
    }
}
