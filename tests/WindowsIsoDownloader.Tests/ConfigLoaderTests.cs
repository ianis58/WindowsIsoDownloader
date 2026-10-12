using WindowsIsoDownloader.Configuration;
using Xunit;

namespace WindowsIsoDownloader.Tests;

public class ConfigLoaderTests
{
    private static string WriteTempConfig(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"wid-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void Load_ReadsValidConfigWithCommentsAndTrailingComma()
    {
        var path = WriteTempConfig(
            """
            {
              // a comment is allowed
              "$schema": "./config.schema.json",
              "DownloadFolder": ".",
              "DownloadFilename": "windows10.iso",
              "PreferredArchitecture": "X64",
              "Attempts": 4,
              "RetryDelaySeconds": 2,
              "Headless": false,
              "Actions": [
                {
                  "Order": 1,
                  "Kind": "Goto",
                  "Parameters": { "Url": "https://www.microsoft.com/en-us/software-download/windows10ISO" },
                },
              ],
            }
            """);

        var config = ConfigLoader.Load(path);

        Assert.Equal("windows10.iso", config.DownloadFilename);
        Assert.Equal(PreferredArchitecture.X64, config.PreferredArchitecture);
        Assert.Equal(4, config.Attempts);
        Assert.Equal(2, config.RetryDelaySeconds);
        Assert.False(config.Headless);
        Assert.Single(config.Actions);
        Assert.Equal(ActionKind.Goto, config.Actions[0].Kind);

        File.Delete(path);
    }

    [Fact]
    public void Load_DefaultsAreApplied()
    {
        var path = WriteTempConfig(
            """
            {
              "Actions": [
                { "Order": 1, "Kind": "Goto", "Parameters": { "Url": "https://example.com" } }
              ]
            }
            """);

        var config = ConfigLoader.Load(path);

        Assert.Equal("windows10.iso", config.DownloadFilename);
        Assert.Equal(PreferredArchitecture.X64, config.PreferredArchitecture);
        Assert.Equal(3, config.Attempts);
        Assert.True(config.Headless);

        File.Delete(path);
    }

    [Fact]
    public void Load_ThrowsWhenFilenameIsAPath()
    {
        var path = WriteTempConfig(
            """
            {
              "DownloadFilename": "../evil.iso",
              "Actions": [
                { "Order": 1, "Kind": "Goto", "Parameters": { "Url": "https://example.com" } }
              ]
            }
            """);

        Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));
        File.Delete(path);
    }

    [Fact]
    public void Load_ThrowsWhenActionIsIncomplete()
    {
        var path = WriteTempConfig(
            """
            {
              "Actions": [
                { "Order": 1, "Kind": "Click", "Parameters": {} }
              ]
            }
            """);

        Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));
        File.Delete(path);
    }

    [Fact]
    public void Load_ThrowsWhenFileMissing()
    {
        Assert.Throws<ConfigException>(() => ConfigLoader.Load("/definitely/not/here.json"));
    }

    [Fact]
    public void Validate_RejectsEmptyActions()
    {
        var config = new Config { Actions = new List<BrowserAction>() };
        Assert.Throws<ConfigException>(() => ConfigLoader.Validate(config));
    }

    [Fact]
    public void Validate_AcceptsWaitForSelector()
    {
        var config = new Config
        {
            Actions = new List<BrowserAction>
            {
                new() { Order = 1, Kind = ActionKind.WaitForSelector, Parameters = new BrowserActionParameters { Selector = "#x" } },
            },
        };

        ConfigLoader.Validate(config);
    }
}
