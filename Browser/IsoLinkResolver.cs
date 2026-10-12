using System.Text.Json;
using Microsoft.Playwright;
using WindowsIsoDownloader.Configuration;

namespace WindowsIsoDownloader.Browser
{
    /// <summary>Finds the ISO download link on the Microsoft download page.</summary>
    public sealed class IsoLinkResolver
    {
        private const int MaxPollAttempts = 10;
        private const int PollIntervalMs = 1000;

        /// <summary>Polls the page until an <c>.iso</c> link appears and returns the preferred one.</summary>
        public async Task<string?> ResolveAsync(
            IPage page,
            IElementHandle? queried,
            PreferredArchitecture architecture,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(page);

            var links = new List<string>();
            for (var attempt = 0; attempt < MaxPollAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                links = await GetIsoLinksAsync(page).ConfigureAwait(false);
                if (links.Count > 0)
                {
                    break;
                }

                await page.WaitForTimeoutAsync(PollIntervalMs).ConfigureAwait(false);
            }

            var preferred = SelectPreferred(links, architecture);
            if (!string.IsNullOrWhiteSpace(preferred))
            {
                return preferred;
            }

            if (queried is not null)
            {
                var href = await queried.EvaluateAsync<string>("element => element.href").ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(href))
                {
                    return href;
                }
            }

            return links.FirstOrDefault();
        }

        /// <summary>
        /// Pure selection logic (unit-testable): prefers the requested architecture,
        /// then falls back to the first candidate.
        /// </summary>
        public static string? SelectPreferred(IEnumerable<string> links, PreferredArchitecture architecture)
        {
            ArgumentNullException.ThrowIfNull(links);

            var candidates = links
                .Where(l => !string.IsNullOrWhiteSpace(l) && l.Contains(".iso", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (candidates.Count == 0)
            {
                return null;
            }

            var preferences = architecture switch
            {
                PreferredArchitecture.X64 => new[] { "x64", "amd64" },
                PreferredArchitecture.X86 => new[] { "x86", "x32", "i386" },
                PreferredArchitecture.Arm64 => new[] { "arm64", "aarch64" },
                _ => Array.Empty<string>(),
            };

            foreach (var preference in preferences)
            {
                var match = candidates.FirstOrDefault(l => l.Contains(preference, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                {
                    return match;
                }
            }

            return candidates[0];
        }

        private static async Task<List<string>> GetIsoLinksAsync(IPage page)
        {
            // Return a JSON string and deserialize it ourselves: Playwright's own
            // EvaluateAsync deserializer does not round-trip a List<string> cleanly.
            var json = await page.EvaluateAsync<string>(
                "() => JSON.stringify(Array.from(document.querySelectorAll('a[href*=\".iso\"]')).map(a => a.href))")
                .ConfigureAwait(false);

            try
            {
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch (JsonException)
            {
                return new List<string>();
            }
        }
    }
}
