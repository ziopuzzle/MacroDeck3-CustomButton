using MacroDeck.Plugin.Testing.Fakes;
using MacroDeck.Ui.Model.Resources;
using MacroDeck.Ui.Runtime;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class ImageRenderingTests
{
    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(int seconds) => now = now.AddSeconds(seconds);
    }
    private sealed class MutableHandler : HttpMessageHandler
    {
        public byte[] Bytes = Png;
        public bool Fail;
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(new HttpResponseMessage(Fail ? System.Net.HttpStatusCode.ServiceUnavailable : System.Net.HttpStatusCode.OK)
            { Content = new ByteArrayContent(Bytes) });
        }
    }
    [Test] public async Task SameUrlRefreshesChangedBytesAndReusesUnchangedResource()
    {
        var context = new FakeIntegrationContext(); var clock = new Clock();
        var handler = new MutableHandler(); using var http = new HttpClient(handler);
        await using var images = new SessionImages(context.UiResources, http, clock: clock);
        const string url = "https://example.invalid/art";
        await Wait(() => images.Resolve("cover", url) != null);
        var first = images.Resolve("cover", url);
        clock.Advance(29); Assert.That(images.NeedsRefresh, Is.False);
        clock.Advance(1); Assert.That(images.NeedsRefresh, Is.True);
        var revision = images.Revision; images.Resolve("cover", url);
        await Wait(() => images.Revision > revision);
        Assert.That(images.Resolve("cover", url), Is.SameAs(first));
        // A different valid PNG makes this a content update at the same URL.
        handler.Bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aX1sAAAAASUVORK5CYII=");
        clock.Advance(30); revision = images.Revision; images.Resolve("cover", url);
        await Wait(() => images.Revision > revision);
        Assert.That(images.Resolve("cover", url), Is.Not.SameAs(first));
        Assert.That(handler.Calls, Is.EqualTo(3));
        images.BeginRender(); clock.Advance(30);
        Assert.That(images.NeedsRefresh, Is.False, "Hidden elements must not keep rendering.");
    }
    [Test] public async Task TransientFailuresBackOffRetainImageAndResetAfterRecovery()
    {
        var context = new FakeIntegrationContext(); var clock = new Clock();
        var handler = new MutableHandler(); using var http = new HttpClient(handler);
        var logs = new List<string>();
        await using var images = new SessionImages(context.UiResources, http, logs.Add, clock: clock);
        const string url = "https://example.invalid/art?secret=hidden";
        await Wait(() => images.Resolve("cover", url) != null);
        var first = images.Resolve("cover", url);
        handler.Fail = true; clock.Advance(30);
        foreach (var delay in new[] { 1, 2, 4, 8, 16, 30, 30 })
        {
            var revision = images.Revision; images.Resolve("cover", url);
            await Wait(() => images.Revision > revision);
            Assert.That(images.Resolve("cover", url), Is.SameAs(first));
            clock.Advance(delay - 1); Assert.That(images.NeedsRefresh, Is.False);
            clock.Advance(1); Assert.That(images.NeedsRefresh, Is.True);
        }
        handler.Fail = false;
        var before = images.Revision; images.Resolve("cover", url);
        await Wait(() => images.Revision > before);
        clock.Advance(30); handler.Fail = true;
        before = images.Revision; images.Resolve("cover", url);
        await Wait(() => images.Revision > before);
        Assert.That(logs.Last(), Does.Contain("retry in 1s"));
        Assert.That(string.Join("\n", logs), Does.Not.Contain("secret").And.Not.Contain("example.invalid"));
        images.Resolve("cover", "https://example.invalid/new");
        await Wait(() => images.Revision > before + 1);
        Assert.That(logs.Last(), Does.Contain("retry in 1s"));
    }
    [Test] public async Task AnArtworkUrlPassedThroughTheUpdateActionMatchesALiteralSource()
    {
        const string source = "/api/music-player/artwork/408e2083d7e2cca5?instanceId=app.macro-deck.webnowplaying%3A%3Abrowser";
        var hub = new DataHub();
        await new UpdateValueAction(hub).CreateExecutor().ExecuteAsync(new MacroDeck.Sdk.Actions.ActionExecutionContext
        {
            Parameters = new Dictionary<string, object>
            {
                ["channel"] = "artwork", ["key"] = "coverUrl", ["value"] = System.Text.Json.JsonSerializer.SerializeToElement(source)
            }
        });
        string? resolved = null;
        new LayoutRenderer("<image id='cover' source='{{coverUrl}}'/>").Render(hub.Snapshot("artwork").Values,
            images: (_, value) => { resolved = value; return null; });
        Assert.That(resolved, Is.EqualTo(source));
    }
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    private sealed class Handler : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { Interlocked.Increment(ref Calls); return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(Png) }); }
    }
    private sealed class DelayedHandler : HttpMessageHandler
    {
        public readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Continue = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly List<string> Requested = new();
        public bool Cancelled;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            lock (Requested) Requested.Add(request.RequestUri!.AbsolutePath);
            if (request.RequestUri!.AbsolutePath == "/slow")
            {
                Started.TrySetResult();
                try { await Continue.Task.WaitAsync(token); }
                catch (OperationCanceledException) { Cancelled = true; throw; }
            }
            return new(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(Png) };
        }
    }
    private static async Task Wait(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++) await Task.Delay(10);
        Assert.That(condition(), Is.True, "Image operation timed out.");
    }
    [Test] public void BoundSourceAndConditionalImagePropertiesProduceValidTrees()
    {
        var context = new FakeIntegrationContext();
        var handle = context.UiResources.RegisterAsync("test", Png, "image/png").GetAwaiter().GetResult();
        string? requested = null;
        var renderer = new LayoutRenderer("<image id='cover' source='{{url}}' size='80%' opacity='0.5' transition='crossfade'><style when='paused == 1' brightness='0.6' saturation='0'/></image>");
        var root = new UiView(ButtonTests.Surface(), renderer.Render(DataHub.ParseValues("{\"url\":\"https://example.invalid/art\",\"paused\":1}"), images: (_, source) => { requested = source; return handle; })).Tree.Root;
        var image = ButtonTests.Nodes(root).Single(n => n.Type == "ui.image");
        Assert.That(requested, Is.EqualTo("https://example.invalid/art"));
        Assert.That(image.Properties["source"].GetProperty("resourceId").GetString(), Is.EqualTo(handle.ResourceId));
        Assert.That(image.Properties["brightness"].GetDouble(), Is.EqualTo(.6));
        Assert.That(image.Properties["saturation"].GetDouble(), Is.Zero);
        Assert.DoesNotThrow(() => new UiView(ButtonTests.Surface(), new LayoutRenderer("<image id='empty'/>").Render(DataHub.ParseValues("{}"))));
    }
    [Test] public async Task ImageCompletionRefreshesWithoutDataChangesAndDownloadsOnlyOnce()
    {
        var context = new FakeIntegrationContext(); var handler = new Handler(); using var http = new HttpClient(handler);
        var images = new SessionImages(context.UiResources, http);
        await using var session = new ButtonSession(ButtonTests.Surface(), new("demo", "<image id='cover' source='https://example.invalid/art'/>", "{}"), null, images: images);
        await Wait(() => ButtonTests.Nodes(session.BuildTree().Root).Any(n => n.Type == "ui.image" && n.Properties.ContainsKey("source")));
        for (var i = 0; i < 20; i++) session.Refresh();
        Assert.That(handler.Calls, Is.EqualTo(1));
    }
    [Test] public async Task SourceReplacementReusesSlotAndBlankClearsIt()
    {
        var context = new FakeIntegrationContext(); var handler = new Handler(); using var http = new HttpClient(handler);
        await using var images = new SessionImages(context.UiResources, http);
        UiResource? first = null;
        await Wait(() => (first = images.Resolve("cover", "https://example.invalid/first")) != null);
        var revision = images.Revision;
        Assert.That(images.Resolve("cover", "https://example.invalid/second"), Is.EqualTo(first), "Retain artwork while its replacement loads for crossfade.");
        await Wait(() => images.Revision > revision);
        var second = images.Resolve("cover", "https://example.invalid/second");
        Assert.That(second!.ResourceId, Is.EqualTo(first!.ResourceId));
        Assert.That(handler.Calls, Is.EqualTo(2));
        Assert.That(images.Resolve("cover", ""), Is.Null);
    }
    [Test] public void ImageCanBeAddedAndRoundTrippedInTheEditorDocument()
    {
        var document = new LayoutDocument("<stack id='root'/>");
        var id = document.Add("root", "image");
        Assert.That(new LayoutDocument(document.Serialize()).Find(id).Attribute("source")!.Value, Is.EqualTo("{{imageUrl}}"));
    }
    [Test] public async Task ReturningToAnEarlierSourceDuringAnUploadReconcilesTheHostSlot()
    {
        var context = new FakeIntegrationContext(); var handler = new DelayedHandler(); using var http = new HttpClient(handler);
        await using var images = new SessionImages(context.UiResources, http);
        await Wait(() => images.Resolve("cover", "https://example.invalid/first") != null);
        images.Resolve("cover", "https://example.invalid/slow");
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        images.Resolve("cover", "https://example.invalid/first");
        handler.Continue.SetResult();
        await Wait(() => { images.Resolve("cover", "https://example.invalid/first"); lock (handler.Requested) return handler.Requested.Count == 3; });
        lock (handler.Requested) Assert.That(handler.Requested, Is.EqualTo(new[] { "/first", "/slow", "/first" }));
    }
    [Test] public async Task ClosingTheDisplayCancelsPendingDownloads()
    {
        var context = new FakeIntegrationContext(); var handler = new DelayedHandler(); using var http = new HttpClient(handler);
        var images = new SessionImages(context.UiResources, http);
        images.Resolve("cover", "https://example.invalid/slow");
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await images.DisposeAsync();
        Assert.That(handler.Cancelled, Is.True);
        Assert.That(images.Resolve("cover", "https://example.invalid/first"), Is.Null);
    }
}

