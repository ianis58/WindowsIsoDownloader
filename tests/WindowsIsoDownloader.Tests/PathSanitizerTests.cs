using WindowsIsoDownloader.Util;
using Xunit;

namespace WindowsIsoDownloader.Tests;

public class PathSanitizerTests
{
    [Theory]
    [InlineData("windows10.iso")]
    [InlineData("win 11 (22H2).iso")]
    [InlineData("a.b.c.iso")]
    public void ValidateFileName_AcceptsPlainNames(string name)
    {
        PathSanitizer.ValidateFileName(name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("../evil.iso")]
    [InlineData("..\\evil.iso")]
    [InlineData("sub/evil.iso")]
    [InlineData("sub\\evil.iso")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\evil.iso")]
    public void ValidateFileName_RejectsPaths(string name)
    {
        Assert.Throws<ArgumentException>(() => PathSanitizer.ValidateFileName(name));
    }

    [Fact]
    public void ResolveOutputPath_CombinesFolderAndName()
    {
        var baseDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "wid-test"));
        var result = PathSanitizer.ResolveOutputPath(baseDir, "windows10.iso");

        Assert.Equal(Path.Combine(baseDir, "windows10.iso"), result);
    }

    [Fact]
    public void ResolveOutputPath_RejectsTraversal()
    {
        Assert.Throws<ArgumentException>(() => PathSanitizer.ResolveOutputPath(Path.GetTempPath(), "../evil.iso"));
    }
}
