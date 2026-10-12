using System.Globalization;
using WindowsIsoDownloader.Configuration;

namespace WindowsIsoDownloader.Cli
{
    /// <summary>Parsed command line arguments. Every option has a safe default.</summary>
    public sealed class CommandLineOptions
    {
        /// <summary>Explicit path to <c>config.json</c> (<c>--config</c>).</summary>
        public string? ConfigPath { get; private set; }

        /// <summary>Full output path for the ISO (<c>--output</c>).</summary>
        public string? OutputPath { get; private set; }

        /// <summary>Overwrite an existing ISO instead of skipping (<c>--force</c>).</summary>
        public bool Force { get; private set; }

        /// <summary>Resolve the link but do not download (<c>--dry-run</c>).</summary>
        public bool DryRun { get; private set; }

        /// <summary>Architecture override (<c>--arch</c>).</summary>
        public PreferredArchitecture? Architecture { get; private set; }

        /// <summary>Force a visible browser window (<c>--headed</c>).</summary>
        public bool? Headless { get; private set; }

        /// <summary>Slow every browser action down by N milliseconds (<c>--slow-mo</c>).</summary>
        public int? SlowMo { get; private set; }

        /// <summary>Print every language offered for the configured edition and exit (<c>--list-languages</c>).</summary>
        public bool ListLanguages { get; private set; }

        /// <summary>Enable the experimental Sentinel warm-up (<c>--experimental-sentinel</c>).</summary>
        public bool ExperimentalSentinel { get; private set; }

        /// <summary>Verbose logging (<c>--verbose</c>).</summary>
        public bool Verbose { get; private set; }

        /// <summary>Show the help text (<c>--help</c>).</summary>
        public bool ShowHelp { get; private set; }

        /// <summary>Parses the process arguments.</summary>
        public static CommandLineOptions Parse(string[] args)
        {
            var options = new CommandLineOptions();

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                string? inlineValue = null;

                var equals = arg.IndexOf('=');
                if (arg.StartsWith("--", StringComparison.Ordinal) && equals > 0)
                {
                    inlineValue = arg[(equals + 1)..];
                    arg = arg[..equals];
                }

                string NextValue(string name)
                {
                    if (inlineValue is not null)
                    {
                        return inlineValue;
                    }

                    if (i + 1 >= args.Length)
                    {
                        throw new ArgumentException($"Option '{name}' requires a value.");
                    }

                    return args[++i];
                }

                switch (arg)
                {
                    case "-h":
                    case "--help":
                        options.ShowHelp = true;
                        break;

                    case "-c":
                    case "--config":
                        options.ConfigPath = NextValue(arg);
                        break;

                    case "-o":
                    case "--output":
                        options.OutputPath = NextValue(arg);
                        break;

                    case "--arch":
                        options.Architecture = ParseArchitecture(NextValue(arg));
                        break;

                    case "-f":
                    case "--force":
                        options.Force = true;
                        break;

                    case "--dry-run":
                        options.DryRun = true;
                        break;

                    case "--headed":
                        options.Headless = false;
                        break;

                    case "--headless":
                        options.Headless = true;
                        break;

                    case "--slow-mo":
                        options.SlowMo = ParseNonNegativeInt(arg, NextValue(arg));
                        break;

                    case "--list-languages":
                        options.ListLanguages = true;
                        break;

                    case "--experimental-sentinel":
                        options.ExperimentalSentinel = true;
                        break;

                    case "-v":
                    case "--verbose":
                        options.Verbose = true;
                        break;

                    default:
                        throw new ArgumentException($"Unknown option '{args[i]}'. Use --help to see the available options.");
                }
            }

            return options;
        }

        private static PreferredArchitecture ParseArchitecture(string value)
        {
            return value.ToLowerInvariant() switch
            {
                "x64" or "amd64" => PreferredArchitecture.X64,
                "x86" or "x32" or "i386" => PreferredArchitecture.X86,
                "arm64" or "aarch64" => PreferredArchitecture.Arm64,
                "any" => PreferredArchitecture.Any,
                _ => throw new ArgumentException($"Invalid architecture '{value}'. Expected x64, x86, arm64 or any."),
            };
        }

        private static int ParseNonNegativeInt(string name, string value)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
            {
                throw new ArgumentException($"Option '{name}' expects a non-negative integer, got '{value}'.");
            }

            return parsed;
        }

        /// <summary>The <c>--help</c> text.</summary>
        public static string HelpText =>
            """
            WindowsIsoDownloader - download the Windows 10/11 ISO from Microsoft.

            Usage:
              WindowsIsoDownloader [options]

            Options:
              -c, --config <path>        Path to config.json (default: next to the executable, then CWD).
              -o, --output <path>        Full path for the ISO file (overrides the config values).
              -f, --force                Overwrite the ISO if it already exists.
                  --dry-run              Resolve the download link but do not download.
                  --arch <x64|x86|arm64|any>
                                         Preferred image architecture (default: x64).
                  --headed               Show the browser window (disables headless mode).
                  --headless             Force headless mode (default).
                  --slow-mo <ms>         Slow down browser actions by the given delay.
                  --list-languages       Print the languages available for the configured edition and exit.
                  --experimental-sentinel
                                         Best-effort warm-up of Microsoft's anti-bot handshake.
              -v, --verbose              Verbose logging.
              -h, --help                 Show this help.

            Exit codes:
              0   success            2   configuration/layout error
              1   unexpected error   3   network error
              130 cancelled          4   anti-bot rejection
              5   disk error         6   browser error
            """;
    }
}
