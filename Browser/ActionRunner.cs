using Microsoft.Playwright;
using WindowsIsoDownloader.Configuration;

namespace WindowsIsoDownloader.Browser
{
    /// <summary>Executes the browser actions defined in <c>config.json</c>.</summary>
    public sealed class ActionRunner
    {
        /// <summary>
        /// Runs every action (ordered by <see cref="BrowserAction.Order"/>). Returns
        /// the element located by the last successful <see cref="ActionKind.QuerySelector"/>.
        /// </summary>
        public async Task<IElementHandle?> RunAsync(
            IPage page,
            IReadOnlyList<BrowserAction> actions,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(page);
            ArgumentNullException.ThrowIfNull(actions);

            IElementHandle? queried = null;

            foreach (var action in actions.OrderBy(a => a.Order))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (action.WaitBeforeAction is > 0)
                {
                    await page.WaitForTimeoutAsync(action.WaitBeforeAction.Value).ConfigureAwait(false);
                }

                try
                {
                    queried = await RunSingleAsync(page, action, queried).ConfigureAwait(false);
                }
                catch (Exception ex) when (action.Optional && BrowserErrors.IsRecoverable(ex))
                {
                    Console.WriteLine($"[Info] Optional action {action.Order} ({action.Kind}) skipped: {ex.Message.Split('\n')[0]}");
                }
            }

            return queried;
        }

        private static async Task<IElementHandle?> RunSingleAsync(IPage page, BrowserAction action, IElementHandle? current)
        {
            var parameters = action.Parameters ?? new BrowserActionParameters();
            var timeout = action.Timeout;

            switch (action.Kind)
            {
                case ActionKind.Goto:
                    await page.GotoAsync(parameters.Url!, new PageGotoOptions { Timeout = timeout }).ConfigureAwait(false);
                    break;

                case ActionKind.SelectOption:
                    await page.SelectOptionAsync(parameters.Selector!, parameters.Values!, new PageSelectOptionOptions { Timeout = timeout }).ConfigureAwait(false);
                    break;

                case ActionKind.Click:
                    await page.ClickAsync(parameters.Selector!, new PageClickOptions { Timeout = timeout }).ConfigureAwait(false);
                    break;

                case ActionKind.WaitForSelector:
                    await page.WaitForSelectorAsync(parameters.Selector!, new PageWaitForSelectorOptions
                    {
                        Timeout = timeout,
                        State = WaitForSelectorState.Attached,
                    }).ConfigureAwait(false);
                    break;

                case ActionKind.QuerySelector:
                    current = await page.QuerySelectorAsync(parameters.Selector!).ConfigureAwait(false);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(action), $"Unknown action kind: {action.Kind}");
            }

            return current;
        }
    }
}
