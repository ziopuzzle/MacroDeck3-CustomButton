using System.Net;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class ImageSourceLoaderTests
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    private sealed class Handler(byte[] content, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public Uri? Requested;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { Requested = request.RequestUri; return Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent(content) }); }
    }
    [Test] public async Task HostArtworkPathUsesTheConfiguredOriginAndPreservesTheInstanceQuery()
    {
        const string source = "/api/music-player/artwork/408e2083d7e2cca5?instanceId=app.macro-deck.webnowplaying%3A%3Abrowser";
        var handler = new Handler(Png); using var http = new HttpClient(handler);
        var image = await ImageSourceLoader.LoadAsync(source, http, 1024, default, new Uri("http://127.0.0.1:43210/ignored/base/"));
        Assert.That(image.MediaType, Is.EqualTo("image/png"));
        Assert.That(handler.Requested!.AbsoluteUri, Is.EqualTo("http://127.0.0.1:43210" + source));
        Assert.ThrowsAsync<FormatException>(() => ImageSourceLoader.LoadAsync(source, http, 1024, default));
    }
    [Test] public async Task AnExternalUrlDoesNotUseTheHostOrigin()
    {
        var handler = new Handler(Png); using var http = new HttpClient(handler);
        await ImageSourceLoader.LoadAsync("https://example.invalid/cover", http, 1024, default, new Uri("http://127.0.0.1:43210"));
        Assert.That(handler.Requested!.Host, Is.EqualTo("example.invalid"));
    }
    [Test] public async Task IconPackReferenceUsesTheVerifiedHostImageEndpoint()
    {
        var handler = new Handler(Png); using var http = new HttpClient(handler);
        const string id = "01a08476-71b1-7f7f-b698-ece4d0db180e";
        await ImageSourceLoader.LoadAsync("icon-pack:" + id, http, 1024, default, new Uri("http://127.0.0.1:43210"));
        Assert.That(handler.Requested!.AbsoluteUri, Is.EqualTo("http://127.0.0.1:43210/api/icons/" + id + "/image"));
        Assert.ThrowsAsync<FormatException>(() => ImageSourceLoader.LoadAsync("icon-pack:../invalid", http, 1024, default));
    }
    [Test] public async Task ReadsFileByContentEvenWithAnUnrelatedExtension()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(path, Png);
            using var http = new HttpClient();
            var image = await ImageSourceLoader.LoadAsync(path, http, 1024, default);
            Assert.That(image.MediaType, Is.EqualTo("image/png")); Assert.That(image.Bytes, Is.EqualTo(Png));
            Assert.ThrowsAsync<FormatException>(() => ImageSourceLoader.LoadAsync(path, http, 20, default));
        }
        finally { File.Delete(path); }
    }
    [Test] public async Task DownloadsBytesAndRejectsHttpErrorsAndOversizedResponses()
    {
        using var http = new HttpClient(new Handler(Png));
        Assert.That((await ImageSourceLoader.LoadAsync("https://example.invalid/cover", http, 1024, default)).Bytes, Is.EqualTo(Png));
        Assert.ThrowsAsync<FormatException>(() => ImageSourceLoader.LoadAsync("https://example.invalid/cover", http, 20, default));
        using var missing = new HttpClient(new Handler(Png, HttpStatusCode.NotFound));
        Assert.ThrowsAsync<HttpRequestException>(() => ImageSourceLoader.LoadAsync("https://example.invalid/cover", missing, 1024, default));
    }
    [TestCase("<svg/>")][TestCase("<html>error</html>")][TestCase("")]
    public void RejectsUnsupportedDownloadedContent(string text)
    {
        using var http = new HttpClient(new Handler(System.Text.Encoding.UTF8.GetBytes(text)));
        Assert.ThrowsAsync<FormatException>(() => ImageSourceLoader.LoadAsync("https://example.invalid/cover.png", http, 1024, default));
    }
    [TestCase("relative.png")][TestCase("data:image/png;base64,AAAA")][TestCase("ftp://example.invalid/image")]
    public void RejectsAmbiguousOrUnsupportedSources(string source)
    {
        using var http = new HttpClient();
        Assert.ThrowsAsync<FormatException>(() => ImageSourceLoader.LoadAsync(source, http, 1024, default));
    }
}

