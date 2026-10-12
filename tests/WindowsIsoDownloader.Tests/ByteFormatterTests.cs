using WindowsIsoDownloader.Util;
using Xunit;

namespace WindowsIsoDownloader.Tests;

public class ByteFormatterTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1024, "1.00 KB")]
    [InlineData(1536, "1.50 KB")]
    [InlineData(1024 * 1024, "1.00 MB")]
    [InlineData(5L * 1024 * 1024 * 1024, "5.00 GB")]
    public void FormatBytes_FormatsExpected(long bytes, string expected)
    {
        Assert.Equal(expected, ByteFormatter.FormatBytes(bytes));
    }

    [Fact]
    public void FormatBytes_NegativeIsUnknown()
    {
        Assert.Equal("?", ByteFormatter.FormatBytes(-1));
    }

    [Theory]
    [InlineData(0, "--")]
    [InlineData(-5, "--")]
    [InlineData(1024, "1.00 KB/s")]
    public void FormatSpeed_FormatsExpected(double speed, string expected)
    {
        Assert.Equal(expected, ByteFormatter.FormatSpeed(speed));
    }

    [Fact]
    public void FormatEta_HandlesUnknownAndKnown()
    {
        Assert.Equal("--:--:--", ByteFormatter.FormatEta(null));
        Assert.Equal("00:00:00", ByteFormatter.FormatEta(TimeSpan.Zero));
        Assert.Equal("00:01:05", ByteFormatter.FormatEta(TimeSpan.FromSeconds(65)));
    }
}
