using System.Net;
using Microsoft.Playwright;
using WindowsIsoDownloader.Browser;
using WindowsIsoDownloader.Cli;
using WindowsIsoDownloader.Configuration;
using WindowsIsoDownloader.Diagnostics;
using WindowsIsoDownloader.Download;
using WindowsIsoDownloader.Util;

namespace WindowsIsoDownloader
{
    /// <summary>Runs the end-to-end ISO download workflow.</summary>
    public sealed class App
    {
        private readonly CommandLineOptions _options;

        public App(CommandLineOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>Executes the workflow and returns a process exit code.</summary>
        public async Task<int> RunAsync(CancellationToken cancellationToken = default)
        {
            Config config;
            string configPath;

            try
            {
                configPath = ConfigLoader.ResolveConfigPath(_options.ConfigPath);
                config = ConfigLoader.Load(configPath);
            }
            catch (ConfigException e)
            {
                Console.WriteLine($"[Fatal] {e.Message}");
                return ExitCodes.ConfigError;
            }

            ApplyOverrides(config);

            string outputPath;
            try
            {
                outputPath = !string.IsNullOrWhiteSpace(_options.OutputPath)
                    ? Path.GetFullPath(_options.OutputPath)
                    : PathSanitizer.ResolveOutputPath(config.DownloadFolder, config.DownloadFilename);
            }
            catch (ArgumentException e)
            {
                Console.WriteLine($"[Fatal] {e.Message}");
                return ExitCodes.ConfigError;
            }

            var outputDirectory = Path.GetDirectoryName(outputPath) ?? Directory.GetCurrentDirectory();

            Console.WriteLine($"[Info] Configuration: {configPath}");
            Console.WriteLine($"[Info] Running on {System.Runtime.InteropServices.RuntimeInformation.OSDescription}.");
            Console.WriteLine($"[Info] The ISO will be saved to: {outputPath}");

            try
            {
                Directory.CreateDirectory(outputDirectory);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Fatal] Could not create the output folder: {e.Message}");
                return ExitCodes.DiskError;
            }

            if (!_options.ListLanguages && !_options.DryRun && File.Exists(outputPath) && !_options.Force)
            {
                Console.WriteLine($"[Info] The file already exists: {outputPath}");
                Console.WriteLine("[Info] Use --force to download it again.");
                return ExitCodes.Success;
            }

            if (!EnsureBrowserAvailable())
            {
                return ExitCodes.BrowserError;
            }

            using var playwright = await Playwright.CreateAsync();

            if (_options.ListLanguages)
            {
                return await RunListLanguagesAsync(playwright, config, cancellationToken).ConfigureAwait(false);
            }

            var resolution = await ResolveIsoUrlAsync(playwright, config, outputDirectory, cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(resolution.Url))
            {
                Console.WriteLine("[Fatal] Could not obtain the download link.");
                if (resolution.SentinelRejected)
                {
                    Console.WriteLine("[Hint] Microsoft's anti-bot protection (Sentinel) rejected the request. Wait a few minutes and try again.");
                    return ExitCodes.AntiBotError;
                }

                Console.WriteLine("[Hint] The page layout may have changed; review the actions in config.json.");
                return ExitCodes.ConfigError;
            }

            Console.WriteLine($"[Success] Download link found: {DescribeUrl(resolution.Url)}");

            if (_options.DryRun)
            {
                Console.WriteLine("[Info] Dry run: skipping the download.");
                return ExitCodes.Success;
            }

            return await DownloadAsync(resolution.Url, outputPath, cancellationToken).ConfigureAwait(false);
        }

        private async Task<ResolutionResult> ResolveIsoUrlAsync(
            IPlaywright playwright,
            Config config,
            string outputDirectory,
            CancellationToken cancellationToken)
        {
            var profileDirectory = GetProfileDirectory();
            var diagnosticsDirectory = Path.Combine(outputDirectory, "diagnostics");
            var sentinelSeen = false;

            for (var attempt = 1; attempt <= config.Attempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Console.WriteLine($"[Info] Attempt {attempt} of {config.Attempts}...");

                IBrowserContext context;
                try
                {
                    context = await LaunchContextAsync(playwright, config, profileDirectory).ConfigureAwait(false);
                }
                catch (Exception e) when (BrowserErrors.IsRecoverable(e))
                {
                    Console.WriteLine($"[Fatal] Could not launch Firefox: {e.Message}");
                    Console.WriteLine("[Hint] On Linux install the required system libraries with:");
                    Console.WriteLine("       playwright install-deps firefox");
                    return new ResolutionResult(null, false, true);
                }

                await using (context)
                {
                    var page = context.Pages.Count > 0 ? context.Pages[0] : await context.NewPageAsync().ConfigureAwait(false);

                    var sentinelRejected = false;
                    page.Response += (_, response) =>
                    {
                        if (sentinelRejected || !response.Url.Contains("GetProductDownloadLinksBySku", StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var body = await response.TextAsync().ConfigureAwait(false);
                                if (body.Contains("SentinelReject", StringComparison.OrdinalIgnoreCase))
                                {
                                    sentinelRejected = true;
                                }
                            }
                            catch (Exception ex) when (BrowserErrors.IsRecoverable(ex))
                            {
                                // Body unavailable.
                            }
                        });
                    };

                    try
                    {
                        if (_options.Verbose || _options.ExperimentalSentinel || config.ExperimentalSentinel)
                        {
                            await WarmUpAsync(page, context, config).ConfigureAwait(false);
                        }

                        var runner = new ActionRunner();
                        var queried = await runner.RunAsync(page, config.Actions, cancellationToken).ConfigureAwait(false);

                        var resolver = new IsoLinkResolver();
                        var url = await resolver
                            .ResolveAsync(page, queried, config.PreferredArchitecture, cancellationToken)
                            .ConfigureAwait(false);

                        if (!string.IsNullOrWhiteSpace(url))
                        {
                            return new ResolutionResult(url, sentinelSeen, false);
                        }

                        // Give the fire-and-forget Sentinel probe a moment to settle.
                        await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                        sentinelSeen |= sentinelRejected;

                        if (sentinelRejected)
                        {
                            Console.WriteLine("[Warn] Microsoft's anti-bot protection (Sentinel) rejected this session.");
                        }

                        await CaptureDiagnosticsAsync(page, diagnosticsDirectory).ConfigureAwait(false);
                    }
                    catch (Exception e) when (BrowserErrors.IsRecoverable(e))
                    {
                        Console.WriteLine($"[Warn] Browser error: {e.Message.Split('\n')[0]}");
                        await CaptureDiagnosticsAsync(page, diagnosticsDirectory).ConfigureAwait(false);
                    }
                }

                if (attempt < config.Attempts)
                {
                    var seconds = Math.Min(60, config.RetryDelaySeconds * Math.Pow(2, attempt - 1)) + Random.Shared.NextDouble();
                    Console.WriteLine($"[Warn] Download link not found. Retrying in {seconds:0.0}s with a fresh session...");
                    await Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken).ConfigureAwait(false);
                }
            }

            return new ResolutionResult(null, sentinelSeen, false);
        }

        private async Task<int> RunListLanguagesAsync(IPlaywright playwright, Config config, CancellationToken cancellationToken)
        {
            var profileDirectory = GetProfileDirectory();

            IBrowserContext context;
            try
            {
                context = await LaunchContextAsync(playwright, config, profileDirectory).ConfigureAwait(false);
            }
            catch (Exception e) when (BrowserErrors.IsRecoverable(e))
            {
                Console.WriteLine($"[Fatal] Could not launch Firefox: {e.Message}");
                return ExitCodes.BrowserError;
            }

            await using (context)
            {
                var page = context.Pages.Count > 0 ? context.Pages[0] : await context.NewPageAsync().ConfigureAwait(false);
                var lister = new LanguageLister();
                var languages = await lister.ListAsync(page, config.Actions).ConfigureAwait(false);

                Console.WriteLine($"[Success] {languages.Count} language(s) available for the configured edition:");
                foreach (var language in languages)
                {
                    Console.WriteLine($"  {language.Text,-35} {language.Value}");
                }

                return ExitCodes.Success;
            }
        }

        private async Task<int> DownloadAsync(string isoUrl, string outputPath, CancellationToken cancellationToken)
        {
            if (!LinkValidator.IsAllowedIsoUrl(isoUrl, out var reason))
            {
                Console.WriteLine($"[Fatal] The resolved link is not trusted: {reason}");
                return ExitCodes.ConfigError;
            }

            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            ConsoleCancelEventHandler handler = (_, e) =>
            {
                e.Cancel = true;
                Console.WriteLine();
                Console.WriteLine("[Warn] Cancellation requested; stopping. Partial data is kept in the .part file.");
                cancellation.Cancel();
            };

            Console.CancelKeyPress += handler;
            try
            {
                using var httpClient = CreateHttpClient();
                var renderer = new ProgressRenderer();
                var progress = new Progress<DownloadProgress>(renderer.Report);

                Console.WriteLine("[Info] ISO download in progress...");

                try
                {
                    var total = await httpClient
                        .DownloadFileAsync(isoUrl, outputPath, progress, cancellation.Token)
                        .ConfigureAwait(false);

                    renderer.Complete(new DownloadProgress(total, total, 0));
                    Console.WriteLine($"[Success] ISO downloaded successfully to {outputPath} ({ByteFormatter.FormatBytes(total)}).");
                    return ExitCodes.Success;
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine();
                    Console.WriteLine("[Warn] Download cancelled. Run the tool again to resume from the .part file.");
                    return ExitCodes.Cancelled;
                }
                catch (UnauthorizedAccessException)
                {
                    Console.WriteLine($"[Fatal] Cannot write to '{outputPath}'.");
                    Console.WriteLine("[Hint] Check the folder permissions, or run the tool with elevated rights.");
                    return ExitCodes.DiskError;
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine($"[Fatal] Network error while downloading: {e.Message}");
                    return ExitCodes.NetworkError;
                }
                catch (IOException e)
                {
                    Console.WriteLine($"[Fatal] I/O error while downloading: {e.Message}");
                    return ExitCodes.DiskError;
                }
            }
            finally
            {
                Console.CancelKeyPress -= handler;
            }
        }

        private async Task WarmUpAsync(IPage page, IBrowserContext context, Config config)
        {
            Console.WriteLine("[Info] Warming up the session with microsoft.com ...");
            try
            {
                await page.GotoAsync("https://www.microsoft.com/", new PageGotoOptions { Timeout = 30000 }).ConfigureAwait(false);
                await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
            }
            catch (Exception e) when (BrowserErrors.IsRecoverable(e))
            {
                // Warm-up is best effort.
            }

            if (_options.ExperimentalSentinel || config.ExperimentalSentinel)
            {
                Console.WriteLine("[Info] Running the experimental Sentinel primer ...");
                await SentinelPrimer.PrimeAsync(context).ConfigureAwait(false);
            }
        }

        private static async Task CaptureDiagnosticsAsync(IPage page, string diagnosticsDirectory)
        {
            var saved = await DiagnosticsWriter.CaptureAsync(page, diagnosticsDirectory).ConfigureAwait(false);
            if (saved is not null)
            {
                Console.WriteLine($"[Info] Diagnostics saved to: {saved}");
            }
        }

        private void ApplyOverrides(Config config)
        {
            if (_options.Architecture is { } architecture)
            {
                config.PreferredArchitecture = architecture;
            }

            if (_options.Headless is { } headless)
            {
                config.Headless = headless;
            }

            if (_options.ExperimentalSentinel)
            {
                config.ExperimentalSentinel = true;
            }
        }

        private async Task<IBrowserContext> LaunchContextAsync(IPlaywright playwright, Config config, string profileDirectory)
        {
            Directory.CreateDirectory(profileDirectory);

            var options = new BrowserTypeLaunchPersistentContextOptions
            {
                Headless = config.Headless,
                Locale = "en-US",
            };

            if (_options.SlowMo is { } slowMo)
            {
                options.SlowMo = slowMo;
            }

            return await playwright.Firefox.LaunchPersistentContextAsync(profileDirectory, options).ConfigureAwait(false);
        }

        private static HttpClient CreateHttpClient()
        {
            var handler = new SocketsHttpHandler
            {
                ConnectTimeout = TimeSpan.FromSeconds(30),
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            };

            // No global timeout: a multi-gigabyte ISO can take a long time on slow links.
            return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        }

        private static string GetProfileDirectory()
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(root))
            {
                root = Path.GetTempPath();
            }

            return Path.Combine(root, "WindowsIsoDownloader", "playwright-profile");
        }

        private static string DescribeUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri)
                ? $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath} (signed URL)"
                : "(unparseable URL)";
        }

        private static bool EnsureBrowserAvailable()
        {
            Console.WriteLine("[Info] Ensuring the Playwright Firefox build is available...");
            try
            {
                var code = Microsoft.Playwright.Program.Main(new[] { "install", "firefox" });
                if (code != 0)
                {
                    Console.WriteLine("[Fatal] Playwright browser installation failed.");
                    Console.WriteLine("[Hint] On Linux install the required system libraries with:");
                    Console.WriteLine("       playwright install-deps firefox");
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Fatal] Playwright browser installation failed: {e.Message}");
                return false;
            }
        }

        private sealed record ResolutionResult(string? Url, bool SentinelRejected, bool BrowserFailed);
    }
}
