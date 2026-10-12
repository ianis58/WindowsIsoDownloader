using WindowsIsoDownloader.Browser;
using Xunit;

namespace WindowsIsoDownloader.Tests;

public class LinkValidatorTests
{
    [Theory]
    [InlineData("https://software.download.prss.microsoft.com/db/Win10_x64.iso?t=signed")]
    [InlineData("https://download.microsoft.com/x/y.iso")]
    public void IsAllowedIsoUrl_AcceptsMicrosoftHosts(string url)
    {
        Assert.True(LinkValidator.IsAllowedIsoUrl(url, out var reason), reason);
    }

    [Theory]
    [InlineData("http://software.download.prss.microsoft.com/x.iso")]
    [InlineData("https://evil.example.com/x.iso")]
    [InlineData("https://microsoft.com.evil.example.com/x.iso")]
    [InlineData("https://software.download.prss.microsoft.com/x.html")]
    [InlineData("not a url")]
    public void IsAllowedIsoUrl_RejectsUntrusted(string url)
    {
        Assert.False(LinkValidator.IsAllowedIsoUrl(url, out _));
    }
}
