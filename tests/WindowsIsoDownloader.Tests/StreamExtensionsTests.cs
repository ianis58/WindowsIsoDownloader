using WindowsIsoDownloader.Download;
using Xunit;

namespace WindowsIsoDownloader.Tests;

public class StreamExtensionsTests
{
    [Fact]
    public async Task CopyToAsync_CopiesEverythingAndReportsProgress()
    {
        var payload = new byte[100_000];
        Random.Shared.NextBytes(payload);

        using var source = new MemoryStream(payload);
        using var destination = new MemoryStream();
        var reported = new List<long>();

        var total = await source.CopyToAsync(destination, 8192, new Progress<long>(reported.Add));

        Assert.Equal(payload.Length, total);
        Assert.Equal(payload, destination.ToArray());
        Assert.NotEmpty(reported);
        Assert.Equal(payload.Length, reported[^1]);
    }

    [Fact]
    public async Task CopyToAsync_RejectsNonReadableSource()
    {
        using var source = new MemoryStream(new byte[8], writable: false);
        using var destination = new MemoryStream();

        // A read-only MemoryStream is still readable; use a write-only stream instead.
        var writeOnly = new WriteOnlyStream();
        await Assert.ThrowsAsync<ArgumentException>(() => writeOnly.CopyToAsync(destination, 16, null));
    }

    private sealed class WriteOnlyStream : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => 0;

        public override long Position { get => 0; set { } }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
        }
    }
}
