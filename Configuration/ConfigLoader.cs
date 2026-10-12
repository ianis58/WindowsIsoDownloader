using System.Text.Json;
using System.Text.Json.Serialization;
using WindowsIsoDownloader.Util;

namespace WindowsIsoDownloader.Configuration
{
    /// <summary>Loads and validates <c>config.json</c>.</summary>
    public static class ConfigLoader
    {
        /// <summary>Serializer settings shared by every load: case-insensitive, tolerant of comments.</summary>
        public static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>
        /// Resolves which config file to use. An explicit path wins; otherwise a
        /// <c>config.json</c> next to the executable is preferred, falling back to
        /// the current working directory.
        /// </summary>
        public static string ResolveConfigPath(string? explicitPath)
        {
            if (!string.IsNullOrWhiteSpace(explicitPath))
            {
                return Path.GetFullPath(explicitPath);
            }

            var nextToExecutable = Path.Combine(AppContext.BaseDirectory, "config.json");
            if (File.Exists(nextToExecutable))
            {
                return nextToExecutable;
            }

            return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "config.json"));
        }

        /// <summary>Reads, deserializes and validates the configuration file.</summary>
        public static Config Load(string path)
        {
            if (!File.Exists(path))
            {
                throw new ConfigException($"Configuration file not found: {path}");
            }

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                throw new ConfigException($"Could not read '{path}': {e.Message}", e);
            }

            Config? config;
            try
            {
                config = JsonSerializer.Deserialize<Config>(json, SerializerOptions);
            }
            catch (JsonException e)
            {
                throw new ConfigException($"Invalid JSON in '{path}': {e.Message}", e);
            }

            if (config is null)
            {
                throw new ConfigException($"The configuration in '{path}' is empty.");
            }

            Validate(config);
            return config;
        }

        /// <summary>Validates the loaded configuration, throwing <see cref="ConfigException"/> on the first problem.</summary>
        public static void Validate(Config config)
        {
            ArgumentNullException.ThrowIfNull(config);

            if (string.IsNullOrWhiteSpace(config.DownloadFolder))
            {
                throw new ConfigException("'DownloadFolder' must not be empty.");
            }

            if (string.IsNullOrWhiteSpace(config.DownloadFilename))
            {
                throw new ConfigException("'DownloadFilename' must not be empty.");
            }

            try
            {
                PathSanitizer.ValidateFileName(config.DownloadFilename);
            }
            catch (ArgumentException e)
            {
                throw new ConfigException($"'DownloadFilename' is invalid: {e.Message}", e);
            }

            if (config.Attempts < 1)
            {
                throw new ConfigException("'Attempts' must be greater than or equal to 1.");
            }

            if (config.RetryDelaySeconds < 0)
            {
                throw new ConfigException("'RetryDelaySeconds' must be greater than or equal to 0.");
            }

            if (config.Actions is null || config.Actions.Count == 0)
            {
                throw new ConfigException("At least one action must be defined in 'Actions'.");
            }

            foreach (var action in config.Actions)
            {
                action.Parameters ??= new BrowserActionParameters();
                var p = action.Parameters;

                switch (action.Kind)
                {
                    case ActionKind.Goto when string.IsNullOrWhiteSpace(p.Url):
                        throw new ConfigException($"Action {action.Order} (Goto) requires 'Parameters.Url'.");

                    case ActionKind.SelectOption when string.IsNullOrWhiteSpace(p.Selector) || string.IsNullOrWhiteSpace(p.Values):
                        throw new ConfigException($"Action {action.Order} (SelectOption) requires 'Parameters.Selector' and 'Parameters.Values'.");

                    case ActionKind.Click when string.IsNullOrWhiteSpace(p.Selector):
                        throw new ConfigException($"Action {action.Order} (Click) requires 'Parameters.Selector'.");

                    case ActionKind.QuerySelector when string.IsNullOrWhiteSpace(p.Selector):
                        throw new ConfigException($"Action {action.Order} (QuerySelector) requires 'Parameters.Selector'.");

                    case ActionKind.WaitForSelector when string.IsNullOrWhiteSpace(p.Selector):
                        throw new ConfigException($"Action {action.Order} (WaitForSelector) requires 'Parameters.Selector'.");
                }
            }
        }
    }
}
