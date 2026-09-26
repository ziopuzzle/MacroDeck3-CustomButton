using System.Text.Json;
using MacroDeck.Ui.Model.Events;
using MacroDeck.Ui.Model.Nodes;
using MacroDeck.Ui.Runtime;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class PublicationPolishTests
{
    private static UiNode Render(string xml) => new UiView(ButtonTests.Surface(), new LayoutRenderer(xml).Render(DataHub.ParseValues("{}"))).Tree.Root;

    [TestCase("rect")][TestCase("circle")][TestCase("capsule")][TestCase("path")]
    public void LocalShapesOccupyParentBoxWithoutBasisSizedSpacers(string type)
    {
        var root = Render($"<stack id='s'><layer id='small' mainSize='20%'><{type} id='shape' coordinates='local' x='10%' y='20%' width='60%' height='50%' color='#ff000080' strokeColor='#fff'/></layer></stack>");
        var nodes = ButtonTests.Nodes(root).ToArray();
        Assert.That(nodes.Any(n => n.Id.Contains("offsetX") || n.Id.Contains("offsetY") || n.Id.EndsWith(".row")), Is.False);
        var shapes = nodes.Where(n => n.Type == "ui.shape").ToArray();
        Assert.That(shapes, Is.Not.Empty);
        Assert.That(shapes.All(n => !n.Properties.ContainsKey("mainSize")), Is.True);
        Assert.That(shapes.All(n => n.Properties["path"].GetString()!.Contains("0.1")), Is.True);
    }
    [TestCase("<rect id='s' coordinates='local' x='80%' width='50%'/>")]
    [TestCase("<rect id='s' coordinates='local' gradient='linear'/>")]
    [TestCase("<path id='s' coordinates='local' width='80%' height='20%' data='M0 0 A0.2 0.3 45 0 1 1 1'/>")]
    public void UnsupportedLocalGeometryIsExplicit(string xml) => Assert.Throws<FormatException>(() => Render(xml));

    [Test] public void LocalPathTransformsCoordinatesButNotArcFlags()
    {
        var path = LocalShapePath.Build("path", .1, .2, .5, .4, 0, "M0 0 C0.1 0.2 0.3 0.4 1 1 A0.1 0.2 0 0 1 0 0 Z");
        Assert.That(path, Is.EqualTo("M0.1 0.2 C0.15 0.28 0.25 0.36 0.6 0.6 A0.05 0.08 0 0 1 0.1 0.2 Z"));
    }
    [Test] public async Task DisablingWholeButtonKeepsSliderEventsButRejectsRootGestures()
    {
        var gestures = new List<string>(); var controls = new List<ControlInput>();
        var settings = TestLayouts.Sample with { WholeButtonInteraction = false, Layout = "<slider id='volume' key='volume' interactive='true'/>" };
        await using var session = new ButtonSession(ButtonTests.Surface(), settings, new DataHub(), onWidgetEvent: gestures.Add, onControlInput: controls.Add);
        var root = session.BuildTree().Root;
        session.Dispatch(new UiEvent { NodeId = "root", Name = "press" });
        var slider = ButtonTests.Nodes(root).Single(n => n.Type == "ui.slider");
        session.Dispatch(new UiEvent { NodeId = slider.Id, Name = "change", Data = JsonSerializer.SerializeToElement(.5) });
        Assert.That(gestures, Is.Empty);
        Assert.That(controls, Has.Count.EqualTo(1));
        Assert.That(controls[0].Value, Is.EqualTo(50));
        Assert.That(ButtonSettings.Read(JsonSerializer.SerializeToElement(new { wholeButtonInteraction = false })).WholeButtonInteraction, Is.False);
        Assert.That(ButtonSettings.Read(JsonSerializer.SerializeToElement(new { })).WholeButtonInteraction, Is.True);
    }
}

