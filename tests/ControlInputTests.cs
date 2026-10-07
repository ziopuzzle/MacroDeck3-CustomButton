using System.Text.Json;
using MacroDeck.Plugin.Testing.Fakes;
using MacroDeck.Ui.Model.Events;
using MacroDeck.Ui.Model.Nodes;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class ControlInputTests
{
    [Test] public async Task TrackpadRestoresActiveContactAfterSessionReplacement()
    {
        var continuity = new AnimationContinuity(); var hub = new DataHub(); var inputs = new List<ControlInput>();
        var settings = ButtonSettings.Default with { Layout = "<trackpad id='pad' interactive='true' showCursorWhileTouching='true'/>" };
        ButtonSession OpenPad() => new(ButtonTests.Surface(), settings, hub, continuity: continuity, onControlInput: inputs.Add);
        void Send(ButtonSession session, string name, object data) => session.Dispatch(new() { NodeId = Find(session, "pad").Id, Name = name, Data = JsonSerializer.SerializeToElement(data) });
        double Opacity(ButtonSession session) => ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id.EndsWith(".cursor.opacity")).Properties["opacity"].GetDouble();
        var first = OpenPad();
        Assert.That(Opacity(first), Is.EqualTo(0));
        Send(first, "pointer-down", new { id = 7, x = .2, y = .3, t = 0, width = 1, height = 1 });
        Assert.That(Opacity(first), Is.EqualTo(1));
        await first.DisposeAsync();
        await using var second = OpenPad();
        Assert.That(Opacity(second), Is.EqualTo(1));
        Send(second, "pointer-move", new { samples = new[] { new { id = 7, x = .4, y = .5, t = 1 } } });
        var position = inputs.Last().Position!;
        Assert.That(position.X, Is.EqualTo(40));
        Assert.That(position.StartX, Is.EqualTo(20));
        Assert.That(position.PreviousY, Is.EqualTo(30));
        Send(second, "pointer-up", new { id = 7, x = .4, y = .5, t = 2 });
        Assert.That(Opacity(second), Is.EqualTo(0));
        Send(second, "pointer-down", new { id = 8, x = .4, y = .5, t = 3, width = 1, height = 1 });
        Assert.That(Opacity(second), Is.EqualTo(1));
        Send(second, "pointer-up", new { id = 8, x = .4, y = .5, t = 4, cancelled = true });
        Assert.That(Opacity(second), Is.EqualTo(0));
        Assert.That(inputs.Count(i => i.EventName == "position-start"), Is.EqualTo(2));
        Assert.That(inputs.Count(i => i.EventName == "position-end"), Is.EqualTo(1));
    }

    [Test] public async Task TrackpadSupportsDescendingAxesAndRetainsGestureHistory()
    {
        var setup = await Open("<trackpad id='pad' leftValue='100' rightValue='-100' topValue='100' bottomValue='0' stepX='10' stepY='5' interactive='true'/>");
        await using var session = setup.Session;
        void Send(string name, object data) => session.Dispatch(new() { NodeId = Find(session, "pad").Id, Name = name, Data = JsonSerializer.SerializeToElement(data) });
        Send("pointer-down", new { id = 1, x = .25, y = .25, t = 0, width = 1, height = 1 });
        Send("pointer-move", new { samples = new[] { new { id = 1, x = .51, y = .61, t = 1 } } });
        Send("pointer-up", new { id = 1, x = 2, y = -1, t = 2 });
        var events = setup.Context.Events.Published.Where(e => e.EventId.StartsWith("element-position-")).ToArray();
        Assert.That(events.Select(e => e.EventId), Is.EqualTo(new[] { "element-position-start", "element-position-changing", "element-position-end" }));
        var start = events[0].Parameters!.Value; var move = events[1].Parameters!.Value; var end = events[2].Parameters!.Value;
        Assert.Multiple(() => {
            Assert.That(start.GetProperty("x").GetDouble(), Is.EqualTo(50));
            Assert.That(start.GetProperty("previousX").GetDouble(), Is.EqualTo(50));
            Assert.That(move.GetProperty("x").GetDouble(), Is.EqualTo(0));
            Assert.That(move.GetProperty("y").GetDouble(), Is.EqualTo(40));
            Assert.That(move.GetProperty("previousY").GetDouble(), Is.EqualTo(75));
            Assert.That(end.GetProperty("x").GetDouble(), Is.EqualTo(-100));
            Assert.That(end.GetProperty("y").GetDouble(), Is.EqualTo(100));
            Assert.That(end.GetProperty("startX").GetDouble(), Is.EqualTo(50));
            Assert.That(end.GetProperty("previousX").GetDouble(), Is.EqualTo(0));
            Assert.That(end.GetProperty("previousY").GetDouble(), Is.EqualTo(40));
        });
        Send("pointer-down", new { id = 2, x = .5, y = .5, t = 3, width = 1, height = 1 });
        var restart = setup.Context.Events.Published.Last(e => e.EventId == "element-position-start").Parameters!.Value;
        Assert.That(restart.GetProperty("startX").GetDouble(), Is.EqualTo(0));
        Assert.That(restart.GetProperty("previousY").GetDouble(), Is.EqualTo(50));
    }

    [Test] public async Task TrackpadFollowsExternalValuesWithoutPublishingInput()
    {
        var setup = await Open("<trackpad id='pad' interactive='true'/>"); await using var session = setup.Session;
        var before = JsonSerializer.Serialize(Find(session, "cursor"));
        setup.Integration.Hub.Update("demo", new Dictionary<string, JsonElement>
        {
            ["x"] = JsonSerializer.SerializeToElement(25), ["y"] = JsonSerializer.SerializeToElement(75)
        });
        session.Refresh();
        var after = JsonSerializer.Serialize(Find(session, "cursor"));
        Assert.That(after, Is.Not.EqualTo(before));
        Assert.That(after, Does.Contain("M0.25 0 V1 M0 0.75 H1"));
        Assert.That(setup.Context.Events.Published.Any(e => e.EventId.StartsWith("element-")), Is.False);
    }

    [TestCase("keyX='same' keyY='same'")]
    [TestCase("leftValue='10' rightValue='10'")]
    [TestCase("stepY='-1'")]
    public void TrackpadRejectsInvalidConfiguration(string attributes)
        => Assert.Throws<FormatException>(() => new LayoutRenderer($"<trackpad id='pad' {attributes}/>").Render(new Dictionary<string, JsonElement>()));

    [Test] public async Task TrackpadKeepsContactAcrossRedrawsAndPublishesBothAxes()
    {
        const string xml = "<trackpad id='pad' keyX='x' keyY='y' leftValue='-100' rightValue='100' stepX='10' interactive='true'/>";
        var setup = await Open(xml); await using var session = setup.Session;
        void Send(string name, object data) => session.Dispatch(new() { NodeId = Find(session, "pad").Id, Name = name, Data = JsonSerializer.SerializeToElement(data) });
        Send("pointer-down", new { id = 1, x = .5, y = .25, t = 0, width = 2, height = .5 });
        Assert.That(setup.Integration.Hub.Snapshot("demo").Values["x"].GetDouble(), Is.EqualTo(-50));
        Assert.That(setup.Integration.Hub.Snapshot("demo").Values["y"].GetDouble(), Is.EqualTo(50));
        Send("pointer-move", new { samples = new[] { new { id = 1, x = 1.22, y = .1, t = 1 }, new { id = 1, x = 1.56, y = .2, t = 2 }, new { id = 2, x = 0.0, y = 0.0, t = 3 } } });
        Assert.That(setup.Integration.Hub.Snapshot("demo").Values["x"].GetDouble(), Is.EqualTo(60));
        Send("pointer-up", new { id = 1, x = 3, y = -1, t = 4 });
        var final = setup.Context.Events.Published.Single(e => e.EventId == "element-position-end").Parameters!.Value;
        Assert.Multiple(() => {
            Assert.That(final.GetProperty("x").GetDouble(), Is.EqualTo(100));
            Assert.That(final.GetProperty("y").GetDouble(), Is.EqualTo(0));
            Assert.That(final.GetProperty("levelX").GetDouble(), Is.EqualTo(1));
            Assert.That(final.GetProperty("keyY").GetString(), Is.EqualTo("y"));
            Assert.That(ButtonEvents.InputEvents(xml, "pad").Select(e => e.Id), Is.EqualTo(new[] { "element-position-start", "element-position-changing", "element-position-end" }));
        });
        Assert.That(setup.Context.Events.Published.Count(e => e.EventId == "element-position-changing"), Is.EqualTo(1));
        Send("pointer-move", new { samples = new[] { new { id = 1, x = 0, y = 0, t = 5 } } });
        Assert.That(setup.Integration.Hub.Snapshot("demo").Values["x"].GetDouble(), Is.EqualTo(100));
    }

    [Test] public async Task TrackpadRejectsUnownedMovesAndCancelsWithoutFinalEvent()
    {
        var setup = await Open("<trackpad id='pad' interactive='true'/>"); await using var session = setup.Session;
        void Send(string name, object data) => session.Dispatch(new() { NodeId = Find(session, "pad").Id, Name = name, Data = JsonSerializer.SerializeToElement(data) });
        Send("pointer-move", new { samples = new[] { new { id = 1, x = 1, y = 1, t = 0 } } });
        Send("pointer-down", new { id = 1, x = .5, y = .5, t = 1, width = 1, height = 1 });
        Send("pointer-down", new { id = 2, x = 0, y = 0, t = 2, width = 1, height = 1 });
        Send("pointer-up", new { id = 1, x = 0, y = 0, t = 3, cancelled = true });
        Assert.That(setup.Context.Events.Published.Count(e => e.EventId.StartsWith("element-")), Is.EqualTo(1));
        Assert.That(setup.Integration.Hub.Snapshot("demo").Values["x"].GetDouble(), Is.EqualTo(50));
    }

    private const string Layout = """
        <stack id="panel">
          <stack id="play" interactive="true" mainSize="30%" background="#12345680"><text id="caption">Play</text></stack>
          <stack id="next" interactive="true" mainSize="30%"><text id="nextCaption">Next</text></stack>
          <slider id="volume" interactive="true" key="gain" min="-20" max="20" step="2" color="#ffffff80" />
          <text id="reading">{{gain}}</text>
        </stack>
        """;
    private static async Task<(CustomButtonIntegration Integration, FakeIntegrationContext Context, ButtonSession Session)> Open(string layout = Layout, string kind = "widget", bool ghost = false, bool sample = false)
    {
        var integration = new CustomButtonIntegration(); var context = new FakeIntegrationContext(); await integration.InitializeAsync(context);
        var surface = ButtonTests.Surface(kind, sample: sample);
        var attributes = new Dictionary<string, JsonElement>(surface.Attributes)
        { ["ghost"] = JsonSerializer.SerializeToElement(ghost), ["data"] = JsonSerializer.SerializeToElement(new { channel = "demo", layout, initialValues = "{\"gain\":0}" }) };
        var session = (ButtonSession)(await integration.CreateSessionAsync(new() { Surface = surface with { Attributes = attributes }, UiModelVersion = 1 }, default))!;
        return (integration, context, session);
    }
    private static UiNode Find(ButtonSession session, string suffix) => ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id.EndsWith("." + suffix));
    [TestCase("dial", "0.75", 75)]
    [TestCase("toggle", "true", 1)]
    [TestCase("segmented", "1", 1)]
    public async Task NewControlsUpdateDataAndPublishChanges(string type, string payload, double expected)
    {
        var contents = type == "segmented" ? "<text id='a'>A</text><text id='b'>B</text>" : "";
        var setup = await Open($"<{type} id='control' key='gain' interactive='true'>{contents}</{type}>");
        await using var session = setup.Session;
        var node = ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Type == "ui." + type);
        session.Dispatch(new() { NodeId = node.Id, Name = "change", Data = JsonDocument.Parse(payload).RootElement.Clone() });
        var value = setup.Integration.Hub.Snapshot("demo").Values["gain"];
        if (type == "toggle") Assert.That(value.GetBoolean(), Is.True);
        else Assert.That(value.GetDouble(), Is.EqualTo(expected));
        var published = setup.Context.Events.Published.Single(e => e.EventId == "element-change").Parameters!.Value;
        Assert.That(published.GetProperty("value").GetDouble(), Is.EqualTo(expected));
        Assert.That(published.GetProperty("elementId").GetString(), Is.EqualTo("control"));
    }

    [TestCase("toggle", "1")][TestCase("toggle", "\"true\"")]
    [TestCase("segmented", "-1")][TestCase("segmented", "2")][TestCase("segmented", "0.5")]
    [TestCase("dial", "1.1")]
    public async Task NewControlsRejectInvalidPayloads(string type, string payload)
    {
        var contents = type == "segmented" ? "<text id='a'>A</text><text id='b'>B</text>" : "";
        var setup = await Open($"<{type} id='control' key='gain' interactive='true'>{contents}</{type}>");
        await using var session = setup.Session;
        var node = ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Type == "ui." + type);
        session.Dispatch(new() { NodeId = node.Id, Name = "change", Data = JsonDocument.Parse(payload).RootElement.Clone() });
        Assert.That(setup.Context.Events.Published.Any(e => e.EventId == "element-change"), Is.False);
    }

    [Test] public async Task SegmentedKeepsHiddenSlotsAndSuppressesChildInteractions()
    {
        const string xml = "<segmented id='mode' key='gain' interactive='true'><stack id='button' interactive='true' visible='false'/><text id='b'>B</text></segmented>";
        var setup = await Open(xml); await using var session = setup.Session;
        var node = ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Type == "ui.segmented");
        Assert.That(node.Children, Has.Count.EqualTo(2));
        Assert.That(ButtonEvents.InputTargets(xml).Select(t => t.Id), Is.EqualTo(new[] { "mode" }));
        Assert.That(ButtonEvents.InputEvents(xml, "mode").Select(e => e.Id), Is.EqualTo(new[] { "element-change" }));
        Assert.Throws<FormatException>(() => ButtonEvents.AddElementEvent(ButtonEvents.Empty, "widget", "element-adjust", "mode", xml));
    }
    [Test] public async Task SeparateButtonsPublishOriginalXmlIdsWithoutTriggeringTheTile()
    {
        var setup = await Open(); await using var session = setup.Session;
        foreach (var id in new[] { "play", "next" })
            foreach (var gesture in new[] { "press-start", "long-press", "press-end", "press" })
                session.Dispatch(new() { NodeId = Find(session, id).Id, Name = gesture });
        var events = setup.Context.Events.Published.Where(e => e.EventId != ButtonEvents.Tick).ToArray();
        Assert.That(events.Select(e => e.EventId), Is.EqualTo(Enumerable.Repeat(new[] { "element-press-start", "element-long-press", "element-press-end", "element-press" }, 2).SelectMany(a => a)));
        Assert.That(events.Select(e => e.Parameters!.Value.GetProperty("elementId").GetString()), Is.EqualTo(Enumerable.Repeat("play", 4).Concat(Enumerable.Repeat("next", 4))));
        Assert.That(events.All(e => e.Parameters!.Value.GetProperty("widgetId").GetString() == "test-widget"), Is.True);
        Assert.That(events.All(e => !e.Parameters!.Value.TryGetProperty("value", out _)), Is.True);
    }
    [Test] public async Task SliderScalesSnapsSharesAndPublishesFinalValueEvenWhenUnchanged()
    {
        var setup = await Open(); await using var session = setup.Session;
        var slider = ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Type == "ui.slider");
        session.Dispatch(new() { NodeId = slider.Id, Name = "adjust", Data = JsonSerializer.SerializeToElement(.775) });
        session.Dispatch(new() { NodeId = slider.Id, Name = "change", Data = JsonSerializer.SerializeToElement(.775) });
        var events = setup.Context.Events.Published.Where(e => e.EventId.StartsWith("element-")).ToArray();
        Assert.That(events.Select(e => e.EventId), Is.EqualTo(new[] { "element-adjust", "element-change" }));
        foreach (var published in events)
        {
            Assert.That(published.Parameters!.Value.GetProperty("value").GetDouble(), Is.EqualTo(12));
            Assert.That(published.Parameters!.Value.GetProperty("level").GetDouble(), Is.EqualTo(.8));
            Assert.That(published.Parameters!.Value.GetProperty("elementId").GetString(), Is.EqualTo("volume"));
        }
        Assert.That(setup.Integration.Hub.Snapshot("demo").Values["gain"].GetDouble(), Is.EqualTo(12));
        var patch = session.DrainPatches().Single();
        Assert.That(ButtonTests.Text(session.BuildTree(), ".reading"), Is.EqualTo("12"));
        Assert.That(patch.Operations.All(p => p.Op == "set-properties"), Is.True);
    }
    [TestCase("-0.1")][TestCase("1.1")][TestCase("null")][TestCase("\"0.5\"")][TestCase("{}")]
    public async Task InvalidSliderPayloadsCannotChangeDataOrPublish(string raw)
    {
        var setup = await Open(); await using var session = setup.Session;
        var slider = ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Type == "ui.slider");
        session.Dispatch(new() { NodeId = slider.Id, Name = "change", Data = JsonDocument.Parse(raw).RootElement.Clone() });
        Assert.That(setup.Context.Events.Published.Any(e => e.EventId.StartsWith("element-")), Is.False);
        Assert.That(ButtonTests.Text(session.BuildTree(), ".reading"), Is.EqualTo("0"));
    }
    [TestCase("preview", false, false)][TestCase("widget", true, false)][TestCase("widget", false, true)]
    public async Task PreviewGhostAndSampleAreNotInteractive(string kind, bool ghost, bool sample)
    {
        var setup = await Open(kind: kind, ghost: ghost, sample: sample); await using var session = setup.Session;
        foreach (var node in ButtonTests.Nodes(session.BuildTree().Root).ToArray())
        {
            session.Dispatch(new() { NodeId = node.Id, Name = "press" });
            session.Dispatch(new() { NodeId = node.Id, Name = "change", Data = JsonSerializer.SerializeToElement(.5) });
        }
        Assert.That(setup.Context.Events.Published.Any(e => e.EventId.StartsWith("element-")), Is.False);
    }
    [Test] public async Task UnknownHiddenDisabledAndStaleNodesCannotPublish()
    {
        var setup = await Open("<stack id='panel'><stack id='hidden' interactive='true' visible='false'/><stack id='off'/><stack id='on' interactive='true'/></stack>");
        await using var session = setup.Session;
        foreach (var id in new[] { "root.panel.hidden", "root.panel.off", "root.missing" }) session.Dispatch(new() { NodeId = id, Name = "press" });
        session.Dispatch(new() { NodeId = Find(session, "on").Id, Name = "change", Data = JsonSerializer.SerializeToElement(.5) });
        session.Dispatch(new() { NodeId = Find(session, "on").Id, Name = "press", Revision = -1 });
        Assert.That(setup.Context.Events.Published.Any(e => e.EventId.StartsWith("element-")), Is.False);
    }
    [TestCase("<slider id='s' interactive='true'/>")]
    [TestCase("<slider id='s' key='value' value='50'/>")]
    [TestCase("<slider id='s' step='-1'/>")]
    public void InvalidBindingsAreExplained(string xml) => Assert.Throws<FormatException>(() => new LayoutRenderer(xml).Render(DataHub.ParseValues("{}")));
}

