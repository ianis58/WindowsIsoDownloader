using Microsoft.Playwright;

namespace WindowsIsoDownloader.Browser
{
    /// <summary>
    /// Best-effort warm-up of Microsoft's anti-bot ("Sentinel") handshake.
    /// <para>
    /// <b>Experimental and unverified.</b> It replays the public <c>vlscppe</c> /
    /// <c>ov-df</c> tag requests that the download page usually performs, sharing
    /// the browser session cookies. It may help in some environments and does
    /// nothing at all in others.
    /// </para>
    /// </summary>
    public static class SentinelPrimer
    {
        private const string OrgId = "y6jn8c31";
        private const string InstanceId = "560dc9f3-1aa5-4a2f-b63c-9e18f8d0e175";

        /// <summary>Runs the warm-up requests; failures are swallowed (best effort).</summary>
        public static async Task PrimeAsync(IBrowserContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var sessionId = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

            try
            {
                await context.APIRequest.GetAsync(
                    $"https://vlscppe.microsoft.com/tags?org_id={OrgId}&session_id={sessionId}").ConfigureAwait(false);

                await context.APIRequest.GetAsync(
                    $"https://ov-df.microsoft.com/mdt.js?instanceId={InstanceId}&PageId=si&session_id={sessionId}").ConfigureAwait(false);
            }
            catch (Exception ex) when (BrowserErrors.IsRecoverable(ex))
            {
                // Best effort only: the download flow continues regardless.
            }
        }
    }
}
