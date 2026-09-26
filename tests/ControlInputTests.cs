using System.Text.Json;
using MacroDeck.Plugin.Testing.Fakes;
using MacroDeck.Ui.Model.Events;
using MacroDeck.Ui.Model.Nodes;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class ControlInputTests
{
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

