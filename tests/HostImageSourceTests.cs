using MacroDeck.Sdk.Ui;
using MacroDeck.Ui.Model.Resources;
using MacroDeck.Ui.Runtime;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class HostImageSourceTests
{
    private const string Path = "/api/music-player/artwork/700fcdbe1bf33578?instanceId=app.macro-deck.webnowplaying%3A%3Abrowser";
    private const string Icon = "01a08476-71b1-7f7f-b698-ece4d0db180e";
    private sealed class Registry : IUiResourceRegistry
    {
        public string? Name, Instance, Artwork;
        public Guid? IconId;
        public List<string> Removed = [];
        public UiResource Handle = new() { ResourceId = "host-image", ContentHash = "test" };
        
        public Task<UiResource> RegisterAsync(string name, ReadOnlyMemory<byte> content, string mediaType, CancellationToken cancellationToken = default) => throw new AssertionException("Host images must not be downloaded and re-uploaded.");
        public Task RemoveAsync(string name, CancellationToken cancellationToken = default) { Removed.Add(name); return Task.CompletedTask; }
        public Task<UiResource> GetIconAsync(Guid iconId, CancellationToken cancellationToken = default)
        { IconId = iconId; return Task.FromResult(Handle); }
        public Task<UiResource?> RegisterMusicPlayerArtworkAsync(string name, string instanceId, string artworkId, CancellationToken cancellationToken = default)
        { Name = name; Instance = instanceId; Artwork = artworkId; return Task.FromResult<UiResource?>(Handle); }
    }
    private sealed class NoHttp : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw new AssertionException("Host images must not use HTTP.");
    }
    private static async Task Ready(SessionImages images, string source)
    {
        for (var i = 0; i < 500 && images.Resolve("cover", source) == null; i++) await Task.Delay(10);
        Assert.That(images.Resolve("cover", source), Is.Not.Null);
    }
    [TestCase("")][TestCase("artwork:")]
    public async Task ArtworkUsesQualifiedPlayerAndNamedResource(string prefix)
    {
        var registry = new Registry(); using var http = new HttpClient(new NoHttp());
        await using (var images = new SessionImages(registry, http))
        {
            await Ready(images, prefix + Path);
            Assert.That(registry.Instance, Is.EqualTo("app.macro-deck.webnowplaying::browser"));
            Assert.That(registry.Artwork, Is.EqualTo("700fcdbe1bf33578"));
            Assert.That(images.ResolveImage("cover", prefix + Path).AspectRatio, Is.NaN);
        }
        Assert.That(registry.Removed, Does.Contain(registry.Name));
    }
    [TestCase("icon-pack:" + Icon)][TestCase("/api/icons/" + Icon + "/image")]
    public async Task InstalledIconUsesHostHandle(string source)
    {
        var registry = new Registry(); using var http = new HttpClient(new NoHttp());
        await using var images = new SessionImages(registry, http);
        await Ready(images, source);
        Assert.That(registry.IconId, Is.EqualTo(Guid.Parse(Icon)));
        Assert.That(images.Resolve("cover", source), Is.SameAs(registry.Handle));
    }
    [TestCase("artwork:wrong")][TestCase("/api/music-player/artwork/id")][TestCase("icon-pack:invalid")]
    public void RejectsMalformedHostSources(string source) => Assert.Throws<FormatException>(() => HostImageSource.Parse(source));
    [Test] public void ExternalUrlsRemainExternal() => Assert.That(HostImageSource.Parse("https://example.org" + Path), Is.Null);
    [Test] public void HostCoverUsesClientFramingWithoutPressEvents()
    {
        var renderer = new LayoutRenderer("<image id='cover' fit='cover' zoom='1.2' offsetX='0.1'/>");
        var root = new UiView(ButtonTests.Surface(), renderer.Render(DataHub.ParseValues("{}"),
            images: (_, _) => new UiResource { ResourceId = "host-image" }, imageAspectRatio: _ => double.NaN)).Tree.Root;
        var image = ButtonTests.Nodes(root).Single(n => n.Type == "ui.button");
        Assert.That(image.Properties["fit"].GetString(), Is.EqualTo("cover"));
        Assert.That(image.Properties["zoom"].GetDouble(), Is.EqualTo(1.2));
        Assert.That(image.Properties.ContainsKey("events"), Is.False);
    }
}
