namespace WindowsIsoDownloader.Browser
{
    /// <summary>Validates that a resolved link is a trusted, HTTPS ISO URL.</summary>
    public static class LinkValidator
    {
        /// <summary>
        /// Checks the URL scheme (HTTPS), the host (a <c>microsoft.com</c> domain)
        /// and the path (an <c>.iso</c> file). Returns <c>false</c> and a human
        /// readable reason when the link is not trusted.
        /// </summary>
        public static bool IsAllowedIsoUrl(string url, out string reason)
        {
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                reason = "The link is not an absolute URL.";
                return false;
            }

            if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                reason = "The link does not use HTTPS.";
                return false;
            }

            var host = uri.Host;
            if (!host.Equals("microsoft.com", StringComparison.OrdinalIgnoreCase)
                && !host.EndsWith(".microsoft.com", StringComparison.OrdinalIgnoreCase))
            {
                reason = $"Unexpected host '{host}'.";
                return false;
            }

            if (!uri.AbsolutePath.Contains(".iso", StringComparison.OrdinalIgnoreCase))
            {
                reason = "The link does not point to an .iso file.";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
