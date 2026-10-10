using MacroDeck.Ui.Model.Events;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class RootBackgroundTests
{
    [TestCase("")]
    [TestCase(" background='transparent'")]
    [TestCase(" background='#12345680'")]
    public async Task NestedTextCanOptIntoShadowWithoutBordersOrWholeWidgetInteraction(string background)
    {
        var settings = new ButtonSettings("demo", $"<stack id='panel'{background}><stack id='inner'><text id='plain'>Plain</text><text id='shadow' shadow='{{{{enabled}}}}'>Shadow</text></stack></stack>", "{\"enabled\":true}") with { WholeButtonInteraction = false };
        var hub = new DataHub();
        var wholeEvents = new List<string>();
        await using var session = new ButtonSession(ButtonTests.Surface(), settings, hub, onWidgetEvent: wholeEvents.Add);
        var root = session.BuildTree().Root;
        Assert.That(root.Type, Is.EqualTo("ui.button"));
        Assert.That(root.Properties.ContainsKey("events"), Is.False);
        var nodes = ButtonTests.Nodes(root).ToArray();
        Assert.That(nodes.Count(n => n.Type == "ui.button"), Is.EqualTo(1));
        Assert.That(nodes.Single(n => n.Id.EndsWith(".plain")).Properties["shadow"].GetBoolean(), Is.False);
        Assert.That(nodes.Single(n => n.Id.EndsWith(".shadow")).Properties["shadow"].GetBoolean(), Is.True);
        session.Dispatch(new UiEvent { NodeId = root.Id, Name = "press" });
        Assert.That(wholeEvents, Is.Empty);
        hub.Update("demo", DataHub.ParseValues("{\"enabled\":false}")); session.Refresh();
        Assert.That(session.BuildTree().Root.Id, Is.EqualTo(root.Id));
        Assert.That(ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id.EndsWith(".shadow")).Properties["shadow"].GetBoolean(), Is.False);
    }

    [Test] public async Task MissingBackgroundUsesTheStandardDefaultAndRetainsGestures()
    {
        var events = new List<string>();
        await using var session = new ButtonSession(ButtonTests.Surface(), new("demo", "<stack id='panel'><text id='label'>Text</text></stack>", "{}"), null, onWidgetEvent: events.Add);
        var root = session.BuildTree().Root;
        Assert.That(root.Type, Is.EqualTo("ui.button"));
        Assert.That(root.Properties["background"].GetString(), Is.EqualTo("#1a1a1a"));
        foreach (var name in new[] { "press", "long-press", "press-start", "press-end" }) session.Dispatch(new UiEvent { NodeId = root.Id, Name = name });
        Assert.That(events, Is.EqualTo(new[] { "short-press", "long-press", "touch-start", "touch-end" }));
    }
    [TestCase("stack")][TestCase("layer")]
    public async Task TransparentRootHasNoOpaqueUnderlayAndKeepsItsContent(string type)
    {
        await using var session = new ButtonSession(ButtonTests.Surface(), new("demo", $"<{type} id='panel' background='transparent'><text id='label'>Visible</text></{type}>", "{}"), null);
        var nodes = ButtonTests.Nodes(session.BuildTree().Root).ToArray();
        Assert.That(nodes.Where(n => n.Properties.ContainsKey("background")).All(n => n.Properties["background"].GetString() == "transparent"), Is.True);
        Assert.That(nodes.Count(n => n.Type == "ui.text"), Is.EqualTo(1));
        Assert.That(nodes.Any(n => n.Properties.TryGetValue("opacity", out var opacity) && opacity.GetDouble() == 0), Is.False);
    }
    [Test] public async Task ConditionalAndBoundBackgroundsRemoveAndRestoreTheDefault()
    {
        var hub = new DataHub();
        await using var session = new ButtonSession(ButtonTests.Surface(), new("demo", "<stack id='panel'><style when='custom == 1' background='{{color}}'/><text id='label'>Visible</text></stack>", "{\"custom\":0,\"color\":\"#ff000080\"}"), hub);
        Assert.That(session.BuildTree().Root.Properties["background"].GetString(), Is.EqualTo("#1a1a1a"));
        hub.Update("demo", DataHub.ParseValues("{\"custom\":1}")); session.Refresh();
        Assert.That(session.BuildTree().Root.Properties["background"].GetString(), Is.EqualTo("transparent"));
        Assert.That(ButtonTests.Nodes(session.BuildTree().Root).Any(n => n.Properties.TryGetValue("opacity", out var o) && Math.Abs(o.GetDouble() - 128 / 255d) < .001), Is.True);
        hub.Update("demo", DataHub.ParseValues("{\"color\":\"transparent\"}")); session.Refresh();
        Assert.That(ButtonTests.Nodes(session.BuildTree().Root).Where(n => n.Properties.ContainsKey("background")).All(n => n.Properties["background"].GetString() == "transparent"), Is.True);
        hub.Update("demo", DataHub.ParseValues("{\"custom\":0}")); session.Refresh();
        Assert.That(session.BuildTree().Root.Properties["background"].GetString(), Is.EqualTo("#1a1a1a"));
    }
    [Test] public async Task TransparentRootWithStaticBorderKeepsTheBorderWithoutDefaultFill()
    {
        await using var session = new ButtonSession(ButtonTests.Surface(), new("demo", "<stack id='panel' background='transparent' borderStyle='static' borderColor='#ffffff'><text id='label'>Visible</text></stack>", "{}"), null);
        var nodes = ButtonTests.Nodes(session.BuildTree().Root).ToArray();
        Assert.That(nodes.Where(n => n.Properties.ContainsKey("background")).All(n => n.Properties["background"].GetString() == "transparent"), Is.True);
        Assert.That(nodes.Any(n => n.Properties.ContainsKey("strokeColor")), Is.True);
    }
    [Test] public async Task LayerPaintsItsOwnTranslucentBackground()
    {
        await using var session = new ButtonSession(ButtonTests.Surface(), new("demo", "<layer id='panel' background='#ff000080'><text id='label'>Visible</text></layer>", "{}"), null);
        var root = session.BuildTree().Root;
        Assert.That(root.Properties["background"].GetString(), Is.EqualTo("transparent"));
        var nodes = ButtonTests.Nodes(root).ToArray();
        Assert.That(nodes.Any(n => n.Type == "ui.shape" && n.Properties["color"].GetString() == "#ff0000"), Is.True);
        Assert.That(nodes.Count(n => n.Type == "ui.text"), Is.EqualTo(1));
    }
}

