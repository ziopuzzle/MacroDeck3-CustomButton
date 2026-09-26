using MacroDeck.Ui.Model.Nodes;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class ShapeTests
{
    private static UiNode Find(ButtonSession session, string suffix) => ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id.EndsWith("." + suffix));
    private static double Size(UiNode node) => node.Properties["mainSize"].GetProperty("basis").GetDouble();

    [TestCase("square")]
    [TestCase("rounded")]
    public async Task RectangleKeepsOffsetsAndDimensions(string corner)
    {
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with { Layout = $"<layer id='canvas'><rect id='shape' x='10%' y='20%' width='40%' height='30%' corner='{corner}' color='#abc'/></layer>" }, null);
        Assert.That(Size(Find(session, "shape.row.offsetX")), Is.EqualTo(.1));
        Assert.That(Size(Find(session, "shape.offsetY")), Is.EqualTo(.2));
        Assert.That(Size(Find(session, "shape.row")), Is.EqualTo(.3));
        Assert.That(Size(Find(session, "shape.row.body")), Is.EqualTo(.4));
        Assert.That(Find(session, "shape.row.body").Properties["color"].GetString(), Is.EqualTo("#aabbcc"));
    }

    [TestCase("horizontal", .7, .03)]
    [TestCase("vertical", .03, .7)]
    public async Task LineUsesLengthAndThicknessOnCorrectAxes(string direction, double width, double height)
    {
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with { Layout = $"<line id='shape' direction='{direction}' length='70%' thickness='3%'/>" }, null);
        Assert.That(Size(Find(session, "shape.row.body")), Is.EqualTo(width));
        Assert.That(Size(Find(session, "shape.row")), Is.EqualTo(height));
    }

    [Test] public async Task DataChangesSizeColourAndVisibility()
    {
        var hub = new DataHub();
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with { Layout = "<rect id='shape' width='{{value}}%' visibleWhen='value > 0'><style when='value >= 80' color='#f00'/></rect>" }, hub);
        hub.Update("demo", DataHub.ParseValues("{\"value\":85}")); session.Refresh();
        Assert.That(Size(Find(session, "shape.row.body")), Is.EqualTo(.85));
        Assert.That(Find(session, "shape.row.body").Properties["color"].GetString(), Is.EqualTo("#ff0000"));
        hub.Update("demo", DataHub.ParseValues("{\"value\":0}")); session.Refresh();
        Assert.That(ButtonTests.Nodes(session.BuildTree().Root).Any(n => n.Id.EndsWith(".body")), Is.False);
    }

    [TestCase("<rect id='x' x='-1'/>")]
    [TestCase("<rect id='x' corner='circle'/>")]
    [TestCase("<line id='x' direction='diagonal'/>")]
    public void InvalidGeometryIsExplained(string xml) => Assert.Throws<FormatException>(() => new LayoutRenderer(xml).Render(DataHub.ParseValues("{}")));

    [Test] public void ZeroWidthHidesRectangle() => Assert.DoesNotThrow(() => new LayoutRenderer("<rect id='x' width='0'/>").Render(DataHub.ParseValues("{}")));
}


