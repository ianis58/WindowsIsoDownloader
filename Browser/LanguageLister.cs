using System.Text.Json;
using Microsoft.Playwright;
using WindowsIsoDownloader.Configuration;

namespace WindowsIsoDownloader.Browser
{
    /// <summary>A language option offered by the download page.</summary>
    public sealed class LanguageOption
    {
        public string Value { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;
    }

    /// <summary>
    /// Implements <c>--list-languages</c>: it navigates to the configured page,
    /// confirms the configured edition and dumps the languages the page offers.
    /// </summary>
    public sealed class LanguageLister
    {
        private const string EditionSelector = "#product-edition";
        private const string EditionConfirmSelector = "#submit-product-edition";
        private const string LanguagesSelector = "#product-languages";
        private const string ConsentSelector = "#onetrust-reject-all-handler";

        /// <summary>Returns the languages available for the edition defined in <paramref name="actions"/>.</summary>
        public async Task<IReadOnlyList<LanguageOption>> ListAsync(IPage page, IReadOnlyList<BrowserAction> actions)
        {
            ArgumentNullException.ThrowIfNull(page);
            ArgumentNullException.ThrowIfNull(actions);

            var ordered = actions.OrderBy(a => a.Order).ToList();

            var url = ordered
                .FirstOrDefault(a => a.Kind == ActionKind.Goto)?
                .Parameters?.Url;

            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ConfigException("The '--list-languages' option needs a 'Goto' action in config.json.");
            }

            var edition = ordered
                .FirstOrDefault(a => a.Kind == ActionKind.SelectOption
                    && string.Equals(a.Parameters?.Selector, EditionSelector, StringComparison.Ordinal))?
                .Parameters?.Values;

            await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded }).ConfigureAwait(false);

            // Dismiss the cookie banner when present; ignore failures.
            try
            {
                await page.ClickAsync(ConsentSelector, new PageClickOptions { Timeout = 2000 }).ConfigureAwait(false);
            }
            catch (Exception ex) when (BrowserErrors.IsRecoverable(ex))
            {
                // No consent banner.
            }

            if (!string.IsNullOrWhiteSpace(edition))
            {
                await page.SelectOptionAsync(EditionSelector, edition).ConfigureAwait(false);
                await page.ClickAsync(EditionConfirmSelector).ConfigureAwait(false);
            }

            await page.WaitForSelectorAsync(
                $"{LanguagesSelector} option",
                new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached, Timeout = 20000 }).ConfigureAwait(false);

            await page.WaitForTimeoutAsync(1500).ConfigureAwait(false);

            var json = await page.EvaluateAsync<string>(
                $"() => JSON.stringify(Array.from(document.querySelectorAll('{LanguagesSelector} option')).map(o => ({{ value: o.value, text: o.text.trim() }})))")
                .ConfigureAwait(false);

            try
            {
                return JsonSerializer.Deserialize<List<LanguageOption>>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<LanguageOption>();
            }
            catch (JsonException)
            {
                return new List<LanguageOption>();
            }
        }
    }
}
