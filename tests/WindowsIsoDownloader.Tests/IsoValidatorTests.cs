using System.Text;
using WindowsIsoDownloader.Download;
using Xunit;

namespace WindowsIsoDownloader.Tests;

public class IsoValidatorTests
{
    private static MemoryStream BuildImage(string signature)
    {
        const long offset = 0x8001;
        var length = (int)(offset + 8);
        var bytes = new byte[length];
        Encoding.ASCII.GetBytes(signature).CopyTo(bytes, (int)offset);
        return new MemoryStream(bytes);
    }

    [Theory]
    [InlineData("CD001")]
    [InlineData("BEA01")]
    [InlineData("NSR02")]
    [InlineData("NSR03")]
    public void LooksLikeIso_AcceptsKnownSignatures(string signature)
    {
        Assert.True(IsoValidator.LooksLikeIso(BuildImage(signature)));
    }

    [Fact]
    public void LooksLikeIso_RejectsGarbage()
    {
        Assert.False(IsoValidator.LooksLikeIso(BuildImage("XXXXX")));
    }

    [Fact]
    public void LooksLikeIso_RejectsTooShort()
    {
        using var stream = new MemoryStream(new byte[16]);
        Assert.False(IsoValidator.LooksLikeIso(stream));
    }

    [Fact]
    public void LooksLikeIso_RestoresStreamPosition()
    {
        using var stream = BuildImage("CD001");
        stream.Position = 3;

        IsoValidator.LooksLikeIso(stream);

        Assert.Equal(3, stream.Position);
    }
}
