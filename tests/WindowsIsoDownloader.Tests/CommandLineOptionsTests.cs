using WindowsIsoDownloader.Cli;
using WindowsIsoDownloader.Configuration;
using Xunit;

namespace WindowsIsoDownloader.Tests;

public class CommandLineOptionsTests
{
    [Fact]
    public void Parse_ReadsLongAndShortOptions()
    {
        var options = CommandLineOptions.Parse(new[]
        {
            "--config", "/tmp/c.json",
            "-o", "/tmp/out.iso",
            "--force",
            "--dry-run",
            "--arch", "x86",
            "--headed",
            "--verbose",
        });

        Assert.Equal("/tmp/c.json", options.ConfigPath);
        Assert.Equal("/tmp/out.iso", options.OutputPath);
        Assert.True(options.Force);
        Assert.True(options.DryRun);
        Assert.Equal(PreferredArchitecture.X86, options.Architecture);
        Assert.False(options.Headless);
        Assert.True(options.Verbose);
    }

    [Fact]
    public void Parse_SupportsEqualsSyntax()
    {
        var options = CommandLineOptions.Parse(new[] { "--config=/etc/config.json", "--arch=arm64" });

        Assert.Equal("/etc/config.json", options.ConfigPath);
        Assert.Equal(PreferredArchitecture.Arm64, options.Architecture);
    }

    [Theory]
    [InlineData("x64", PreferredArchitecture.X64)]
    [InlineData("amd64", PreferredArchitecture.X64)]
    [InlineData("x32", PreferredArchitecture.X86)]
    [InlineData("aarch64", PreferredArchitecture.Arm64)]
    [InlineData("any", PreferredArchitecture.Any)]
    public void Parse_ParsesArchitectureAliases(string value, PreferredArchitecture expected)
    {
        var options = CommandLineOptions.Parse(new[] { "--arch", value });
        Assert.Equal(expected, options.Architecture);
    }

    [Fact]
    public void Parse_ReadsFlags()
    {
        var options = CommandLineOptions.Parse(new[] { "--list-languages", "--experimental-sentinel", "--help" });

        Assert.True(options.ListLanguages);
        Assert.True(options.ExperimentalSentinel);
        Assert.True(options.ShowHelp);
    }

    [Fact]
    public void Parse_UnknownOptionThrows()
    {
        Assert.Throws<ArgumentException>(() => CommandLineOptions.Parse(new[] { "--nope" }));
    }

    [Fact]
    public void Parse_MissingValueThrows()
    {
        Assert.Throws<ArgumentException>(() => CommandLineOptions.Parse(new[] { "--config" }));
    }

    [Fact]
    public void Parse_InvalidArchitectureThrows()
    {
        Assert.Throws<ArgumentException>(() => CommandLineOptions.Parse(new[] { "--arch", "sparc" }));
    }

    [Fact]
    public void Parse_InvalidSlowMoThrows()
    {
        Assert.Throws<ArgumentException>(() => CommandLineOptions.Parse(new[] { "--slow-mo", "-1" }));
    }
}
