using WindowsIsoDownloader.Browser;
using WindowsIsoDownloader.Configuration;
using Xunit;

namespace WindowsIsoDownloader.Tests;

public class IsoLinkResolverTests
{
    private static readonly string[] Windows10Links =
    {
        "https://software.download.prss.microsoft.com/db/Win10_22H2_English_x32v1.iso?t=abc",
        "https://software.download.prss.microsoft.com/db/Win10_22H2_English_x64v1.iso?t=def",
    };

    [Fact]
    public void SelectPreferred_PrefersX64()
    {
        Assert.Contains("x64", IsoLinkResolver.SelectPreferred(Windows10Links, PreferredArchitecture.X64));
    }

    [Fact]
    public void SelectPreferred_PrefersX86WhenRequested()
    {
        Assert.Contains("x32", IsoLinkResolver.SelectPreferred(Windows10Links, PreferredArchitecture.X86));
    }

    [Fact]
    public void SelectPreferred_FallsBackToFirstWhenArchitectureMissing()
    {
        var links = new[] { "https://host/a.iso", "https://host/b.iso" };
        Assert.Equal(links[0], IsoLinkResolver.SelectPreferred(links, PreferredArchitecture.Arm64));
    }

    [Fact]
    public void SelectPreferred_AnyReturnsFirst()
    {
        Assert.Equal(Windows10Links[0], IsoLinkResolver.SelectPreferred(Windows10Links, PreferredArchitecture.Any));
    }

    [Fact]
    public void SelectPreferred_IgnoresNonIsoLinks()
    {
        var links = new[] { "https://host/page.html", "https://host/image.iso" };
        Assert.Equal("https://host/image.iso", IsoLinkResolver.SelectPreferred(links, PreferredArchitecture.X64));
    }

    [Fact]
    public void SelectPreferred_ReturnsNullWhenNoIso()
    {
        Assert.Null(IsoLinkResolver.SelectPreferred(new[] { "https://host/page.html" }, PreferredArchitecture.X64));
    }

    [Fact]
    public void SelectPreferred_Arm64()
    {
        var links = new[]
        {
            "https://host/Win11_x64.iso",
            "https://host/Win11_arm64.iso",
        };

        Assert.Contains("arm64", IsoLinkResolver.SelectPreferred(links, PreferredArchitecture.Arm64));
    }
}
