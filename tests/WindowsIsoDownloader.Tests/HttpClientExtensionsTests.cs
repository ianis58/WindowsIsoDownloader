using System.Net;
using System.Net.Http.Headers;
using WindowsIsoDownloader.Download;
using Xunit;

namespace WindowsIsoDownloader.Tests;

public class HttpClientExtensionsTests : IDisposable
{
    private readonly string _directory;

    public HttpClientExtensionsTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "wid-download-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Best effort cleanup.
        }
    }

    [Fact]
    public async Task DownloadFileAsync_WritesFileAndRemovesPartFile()
    {
        var payload = CreatePayload(10_000);
        using var client = new HttpClient(new FakeHandler(_ => BinaryResponse(HttpStatusCode.OK, payload)));
        var path = Path.Combine(_directory, "windows.iso");

        var total = await client.DownloadFileAsync("https://host/x.iso", path);

        Assert.Equal(payload.Length, total);
        Assert.Equal(payload, await File.ReadAllBytesAsync(path));
        Assert.False(File.Exists(path + ".part"));
    }

    [Fact]
    public async Task DownloadFileAsync_ResumesFromPartFile()
    {
        var payload = CreatePayload(40_000);
        var path = Path.Combine(_directory, "windows.iso");
        var partPath = path + ".part";
        var alreadyDownloaded = 15_000;
        await File.WriteAllBytesAsync(partPath, payload[..alreadyDownloaded]);

        long? requestedFrom = null;
        using var client = new HttpClient(new FakeHandler(request =>
        {
            requestedFrom = request.Headers.Range?.Ranges.FirstOrDefault()?.From;
            return BinaryResponse(HttpStatusCode.PartialContent, payload[alreadyDownloaded..]);
        }));

        var total = await client.DownloadFileAsync("https://host/x.iso", path);

        Assert.Equal(alreadyDownloaded, requestedFrom);
        Assert.Equal(payload.Length, total);
        Assert.Equal(payload, await File.ReadAllBytesAsync(path));
        Assert.False(File.Exists(partPath));
    }

    [Fact]
    public async Task DownloadFileAsync_RestartsWhenServerIgnoresRange()
    {
        var payload = CreatePayload(20_000);
        var path = Path.Combine(_directory, "windows.iso");
        await File.WriteAllBytesAsync(path + ".part", new byte[5_000]);

        using var client = new HttpClient(new FakeHandler(_ => BinaryResponse(HttpStatusCode.OK, payload)));

        var total = await client.DownloadFileAsync("https://host/x.iso", path);

        Assert.Equal(payload.Length, total);
        Assert.Equal(payload, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task DownloadFileAsync_RejectsHtmlErrorPages()
    {
        var path = Path.Combine(_directory, "windows.iso");
        using var client = new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>blocked</html>"),
        }));

        await Assert.ThrowsAsync<InvalidDataException>(() => client.DownloadFileAsync("https://host/x.iso", path));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task DownloadFileAsync_ThrowsOnFailureStatus()
    {
        var path = Path.Combine(_directory, "windows.iso");
        using var client = new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.DownloadFileAsync("https://host/x.iso", path));
    }

    [Fact]
    public async Task DownloadFileAsync_FinalizesACompletePartFile()
    {
        var payload = CreatePayload(30_000);
        var path = Path.Combine(_directory, "windows.iso");
        var partPath = path + ".part";
        await File.WriteAllBytesAsync(partPath, payload);

        using var client = new HttpClient(new FakeHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                Content = new ByteArrayContent(Array.Empty<byte>()),
            };
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(payload.Length);
            return response;
        }));

        var total = await client.DownloadFileAsync("https://host/x.iso", path);

        Assert.Equal(payload.Length, total);
        Assert.Equal(payload, await File.ReadAllBytesAsync(path));
        Assert.False(File.Exists(partPath));
    }

    private static byte[] CreatePayload(int size)
    {
        var payload = new byte[size];
        Random.Shared.NextBytes(payload);
        return payload;
    }

    private static HttpResponseMessage BinaryResponse(HttpStatusCode status, byte[] body)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new ByteArrayContent(body),
        };

        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return response;
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }
}
