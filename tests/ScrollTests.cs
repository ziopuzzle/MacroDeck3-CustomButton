using System.Text.Json;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class ScrollTests
{
    [TestCase("vertical", "widget")]
    [TestCase("horizontal", "widget")]
    [TestCase("vertical", "dialog")]
    [TestCase("horizontal", "dialog")]
    public async Task PartialScrollKeepsViewportAndLiveContent(string direction, string surface)
    {
        var hub = new DataHub();
        var xml = $"<stack id='root'><text id='header' size='10%'>Fixed</text><scroll id='viewport' direction='{direction}' mainSize='60%' padding='2%' gap='3%' background='{{{{background}}}}'><stack id='content' direction='{direction}'><text id='value'>{{{{value}}}}</text><stack id='tap' mainSize='70%' interactive='true'><text id='label'>Tap</text></stack></stack></scroll></stack>";
        var inputs = new List<ControlInput>();
        await using var session = new ButtonSession(ButtonTests.Surface(surface), new("demo", xml, "{\"value\":1,\"background\":\"#123456\"}") { WholeButtonInteraction = false }, hub, onControlInput: inputs.Add);
        var tree = session.BuildTree();
        var viewport = ButtonTests.Nodes(tree.Root).Single(n => n.Type == "ui.list");
        Assert.That(viewport.Properties["mainSize"].GetProperty("basis").GetDouble(), Is.EqualTo(.6));
        Assert.That(viewport.Properties["direction"].GetString(), Is.EqualTo(direction));
        Assert.That(ButtonTests.Nodes(tree.Root).Single(n => n.Id.EndsWith(".tap")).Properties["mainSize"].GetProperty("basis").GetDouble(), Is.EqualTo(.7));
        var tap = ButtonTests.Nodes(tree.Root).Single(n => n.Id.EndsWith(".tap"));
        session.Dispatch(new() { NodeId = tap.Id, Name = "press" });
        Assert.That(inputs.Single().ElementId, Is.EqualTo("tap"));
        hub.Update("demo", DataHub.ParseValues("{\"value\":2}"));
        session.Refresh();
        var patches = session.DrainPatches();
        Assert.That(patches.SelectMany(p => p.Operations).Any(p => p.Op == "replace-node" && (p.NodeId == viewport.Id || p.NodeId == tree.Root.Id)), Is.False);
        Assert.That(ButtonTests.Text(session.BuildTree(), ".value"), Is.EqualTo("2"));
    }

    [Test]
    public void EditorCanAddAndMoveChildrenIntoScrollAndUndo()
    {
        var history = new LayoutEditHistory("<stack id='root'><text id='label'>Text</text></stack>");
        string scroll = "";
        history.Edit(d => scroll = d.Add("root", "scroll"));
        history.Edit(d => { d.Place("label", scroll, "inside"); return "label"; });
        Assert.That(new LayoutDocument(history.Xml).Find("label").Parent!.Name.LocalName, Is.EqualTo("scroll"));
        history.Undo();
        Assert.That(new LayoutDocument(history.Xml).Find("label").Parent!.Name.LocalName, Is.EqualTo("stack"));
        Assert.That(LayoutOptions.Choices("scroll", "direction"), Is.EqualTo(new[] { "vertical", "horizontal" }));
    }
}
