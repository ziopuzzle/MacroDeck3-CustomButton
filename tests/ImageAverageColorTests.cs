using System.Net;
using MacroDeck.Sdk.Ui;
using MacroDeck.Sdk.MusicPlayer;
using MacroDeck.Ui.Model.Resources;
using NUnit.Framework;
using SkiaSharp;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Variables;

namespace Ziopuzzle.CustomButton.Tests;

public class ImageAverageColorTests
{
    private static byte[] Png(params SKColor[] colors)
    {
        using var bitmap = new SKBitmap(colors.Length, 1);
        for (var x = 0; x < colors.Length; x++) bitmap.SetPixel(x, 0, colors[x]);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
    [Test] public void TransparencyDoesNotDarkenVisiblePixels()
    {
        Assert.That(ImageAverageColor.Calculate(Png(SKColors.Red, SKColors.Transparent)), Is.EqualTo("#ff0000"));
        Assert.That(ImageAverageColor.Calculate(Png(SKColors.Transparent)), Is.EqualTo("#00000000"));
        Assert.That(ImageAverageColor.Calculate(Png(SKColors.Red, SKColors.Blue)), Is.EqualTo("#800080"));
        Assert.Throws<FormatException>(() => ImageAverageColor.Calculate([1, 2, 3]));
    }
    [Test] public void OutputDefaultsAndValidation()
    {
        var renderer = new LayoutRenderer("<image id='img' averageColorKey='mean' averageColorFallback='#1234'/>");
        Assert.That(renderer.ImageColorDefaults["mean"].GetString(), Is.EqualTo("#11223344"));
        Assert.Throws<FormatException>(() => new LayoutRenderer("<layer id='root'><image id='a' averageColorKey='same'/><image id='b' averageColorKey='same'/></layer>"));
        Assert.Throws<FormatException>(() => new LayoutRenderer("<image id='img' averageColorKey='{{name}}'/>"));
        Assert.Throws<FormatException>(() => new LayoutRenderer("<image id='img'><style when='true' averageColorKey='c'/></image>"));
        var icon = new LayoutRenderer("<image id='img' averageColorKey='mean' source='icon-pack:01a08476-71b1-7f7f-b698-ece4d0db180e'/>");
        Assert.That(Assert.Throws<FormatException>(() => icon.Render(DataHub.ParseValues("{}")))!.Message, Does.Contain("Icon Pack"));
    }
    [TestCase(false)] [TestCase(true)]
    public async Task ResultPublishesOnlyForLiveSessionsAndCanBeWrittenToVariables(bool publish)
    {
        using var http = new HttpClient(new Handler(Png(SKColors.Red)));
        var images = new SessionImages(new Registry(), http);
        var hub = new DataHub();
        await using var session = new ButtonSession(ButtonTests.Surface(), new("test",
            "<stack id='root'><image id='img' source='https://example.invalid/a' averageColorKey='mean'/><text id='value'>{{mean}}</text></stack>", "{}"), hub, images: images, publishImageColors: publish);
        await Until(() => images.Colors().GetValueOrDefault("mean") == "#ff0000");
        session.Refresh();
        Assert.That(ButtonTests.Text(session.BuildTree(), ".value"), Is.EqualTo("#ff0000"));
        if (!publish) { Assert.That(hub.Read("test"), Is.Empty); return; }
        Assert.That(hub.Read("test")["mean"].GetString(), Is.EqualTo("#ff0000"));
        var variables = new Variables();
        var result = await new WriteDisplayVariableAction(hub, () => variables).CreateExecutor().ExecuteAsync(new()
        { Parameters = new Dictionary<string, object> { ["channel"] = "test", ["key"] = "mean", ["variable"] = "coverColor" } });
        Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Succeeded));
        Assert.That(variables.Value, Is.EqualTo("#ff0000"));
        var revision = hub.Revision("test"); session.Refresh(); session.Refresh();
        Assert.That(hub.Revision("test"), Is.EqualTo(revision), "Unchanged generated values must not loop through Channel updates.");
    }
    [Test] public async Task SvgAndArtworkUseTheirSupportedSources()
    {
        using var http = new HttpClient(new Handler([]));
        await using var images = new SessionImages(new Registry(), http, players: new Players());
        images.ConfigureColor("svg", "svgColor", "transparent");
        var source = SvgTemplate.Source("<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><rect width='8' height='8' fill='#ff0000'/></svg>", s => s, 64);
        images.Resolve("svg", source);
        await Until(() => images.Colors().GetValueOrDefault("svgColor") == "#ff0000");
        images.ConfigureColor("art", "artColor", "transparent");
        images.Resolve("art", "/api/music-player/artwork/id?instanceId=provider%3A%3Aplayer");
        await Until(() => images.Colors().GetValueOrDefault("artColor") == "#123456");
    }
    [Test] public async Task CompetingLiveOutputsDoNotCreateAnUpdateLoop()
    {
        using var redHttp = new HttpClient(new Handler(Png(SKColors.Red)));
        using var blueHttp = new HttpClient(new Handler(Png(SKColors.Blue)));
        var redImages = new SessionImages(new Registry(), redHttp);
        var blueImages = new SessionImages(new Registry(), blueHttp);
        var hub = new DataHub();
        var settings = new ButtonSettings("shared", "<image id='img' source='https://example.invalid/a' averageColorKey='mean'/>", "{}");
        await using var red = new ButtonSession(ButtonTests.Surface(), settings, hub, images: redImages, publishImageColors: true);
        await using var blue = new ButtonSession(ButtonTests.Surface(), settings, hub, images: blueImages, publishImageColors: true);
        await Until(() => redImages.CompletedColors().Count > 0 && blueImages.CompletedColors().Count > 0);
        red.Refresh(); blue.Refresh();
        var revision = hub.Revision("shared");
        for (var i = 0; i < 4; i++) { red.Refresh(); blue.Refresh(); }
        Assert.That(hub.Revision("shared"), Is.EqualTo(revision));
    }
    [Test] public async Task ObsoleteLoadCannotPublishItsColor()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new DelayedHandler(entered, release));
        await using var images = new SessionImages(new Registry(), http);
        images.ConfigureColor("img", "mean", "transparent");
        images.Resolve("img", "https://example.invalid/old");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        images.Resolve("img", "https://example.invalid/new");
        release.SetResult();
        await Until(() => images.Revision > 0);
        Assert.That(images.Colors()["mean"], Is.EqualTo("#00000000"));
        images.Resolve("img", "https://example.invalid/new");
        await Until(() => images.Colors()["mean"] == "#0000ff");
        images.Resolve("img", "");
        await Until(() => images.Colors()["mean"] == "#00000000");
    }
    [Test] public async Task SameUrlContentReplacementRefreshesColorAndHiddenElementsKeepLastResult()
    {
        var clock = new Clock(); var handler = new MutableHandler { Bytes = Png(SKColors.Red) };
        using var http = new HttpClient(handler);
        await using var images = new SessionImages(new Registry(), http, clock: clock);
        images.ConfigureColor("img", "mean", "transparent");
        const string url = "https://example.invalid/unchanged";
        images.Resolve("img", url);
        await Until(() => images.Colors()["mean"] == "#ff0000");
        images.BeginRender();
        Assert.That(images.Colors()["mean"], Is.EqualTo("#ff0000"));
        handler.Bytes = Png(SKColors.Blue); clock.Now += TimeSpan.FromSeconds(31);
        images.Resolve("img", url);
        await Until(() => images.Colors()["mean"] == "#0000ff");
    }
    [Test] public async Task ArtworkColorRefreshIsSharedAndDoesNotReloadImages()
    {
        using var http = new HttpClient(new Handler([]));
        var players = new Players(); var shared = new ArtworkColorCache(() => players);
        var firstRegistry = new Registry(); var secondRegistry = new Registry();
        await using var first = new SessionImages(firstRegistry, http, readArtworkColor: shared.ReadAsync);
        await using var second = new SessionImages(secondRegistry, http, readArtworkColor: shared.ReadAsync);
        const string source = "/api/music-player/artwork/id?instanceId=provider%3A%3Aplayer";
        foreach (var images in new[] { first, second }) { images.ConfigureColor("art", "mean", "transparent"); images.Resolve("art", source); }
        await Until(() => first.Colors()["mean"] == "#123456" && second.Colors()["mean"] == "#123456");
        Assert.That(players.Calls, Is.EqualTo(1));
        players.Color = "#abcdef";
        await Until(() =>
        {
            first.Resolve("art", source); second.Resolve("art", source);
            return first.Colors()["mean"] == "#abcdef" && second.Colors()["mean"] == "#abcdef";
        });
        Assert.That(players.Calls, Is.EqualTo(2));
        Assert.That(firstRegistry.ArtworkReads, Is.EqualTo(1));
        Assert.That(secondRegistry.ArtworkReads, Is.EqualTo(1));
        first.BeginRender(); second.BeginRender();
        await Task.Delay(1100);
        Assert.That(first.NeedsRefresh || second.NeedsRefresh, Is.False);
        Assert.That(players.Calls, Is.EqualTo(2));
    }
    private static async Task Until(Func<bool> ready)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!ready()) await Task.Delay(10, timeout.Token);
    }
    [Test] public async Task OldPlayerColorCannotOverwriteTheNewSource()
    {
        using var http = new HttpClient(new Handler([]));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var images = new SessionImages(new Registry(), http, readArtworkColor: async (player, ct) =>
        {
            if (player == "old") { entered.TrySetResult(); return await release.Task.WaitAsync(ct); }
            return "#0000ff";
        });
        images.ConfigureColor("art", "mean", "transparent");
        images.Resolve("art", "/api/music-player/artwork/id?instanceId=old");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        images.Resolve("art", "/api/music-player/artwork/id?instanceId=new");
        release.SetResult("#ff0000");
        await Until(() => images.NeedsRefresh);
        Assert.That(images.Colors()["mean"], Is.Not.EqualTo("#ff0000"));
        await Until(() =>
        {
            images.Resolve("art", "/api/music-player/artwork/id?instanceId=new");
            return images.Colors()["mean"] == "#0000ff";
        });
    }
    [Test] public async Task ClosingOneCallerDoesNotCancelSharedArtworkRead()
    {
        var api = new WaitingPlayer(); var cache = new ArtworkColorCache(() => api);
        using var cancel = new CancellationTokenSource();
        var first = cache.ReadAsync("player", cancel.Token);
        var second = cache.ReadAsync("player", default);
        await api.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        cancel.Cancel();
        Assert.ThrowsAsync<TaskCanceledException>(async () => await first);
        api.Result.SetResult("#123456");
        Assert.That(await second, Is.EqualTo("#123456"));
        Assert.That(api.Calls, Is.EqualTo(1));
    }
    private sealed class Handler(byte[] bytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
    }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class MutableHandler : HttpMessageHandler
    {
        public byte[] Bytes = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Bytes) });
    }
    private sealed class DelayedHandler(TaskCompletionSource entered, TaskCompletionSource release) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath == "/old") { entered.TrySetResult(); await release.Task.WaitAsync(ct); }
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(Png(request.RequestUri.AbsolutePath == "/old" ? SKColors.Red : SKColors.Blue)) };
        }
    }
    private sealed class Registry : IUiResourceRegistry
    {
        public int ArtworkReads;
        public Task<UiResource> RegisterAsync(string name, ReadOnlyMemory<byte> bytes, string mediaType, CancellationToken cancellationToken = default)
            => Task.FromResult(new UiResource { ResourceId = name });
        public Task RemoveAsync(string name, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<UiResource?> RegisterMusicPlayerArtworkAsync(string name, string instanceId, string artworkId, CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref ArtworkReads); return Task.FromResult<UiResource?>(new() { ResourceId = name }); }
    }
    private sealed class Players : IMusicPlayerApi
    {
        public string Color = "#123456";
        public int Calls;
        public IReadOnlyList<MusicPlayerInstanceSnapshot> Instances => [];
        public event EventHandler? InstancesChanged { add { } remove { } }
        public Task<string?> GetAlbumColorAsync(string instanceId, CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref Calls); return Task.FromResult<string?>(Color); }
    }
    private sealed class Variables : IUserVariableApi
    {
        public string? Value;
        public Task<UserVariableWriteResult> ApplyAsync(string name, string? ownerWidgetId, UserVariableOperation operation, string? value, CancellationToken cancellationToken = default)
        { Value = value; return Task.FromResult(new UserVariableWriteResult(UserVariableWriteStatus.Applied)); }
    }
    private sealed class WaitingPlayer : IMusicPlayerApi
    {
        public int Calls;
        public TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<string?> Result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IReadOnlyList<MusicPlayerInstanceSnapshot> Instances => [];
        public event EventHandler? InstancesChanged { add { } remove { } }
        public Task<string?> GetAlbumColorAsync(string instanceId, CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref Calls); Entered.TrySetResult(); return Result.Task.WaitAsync(cancellationToken); }
    }
}
