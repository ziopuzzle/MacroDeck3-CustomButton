using MacroDeck.Ui.Model.Nodes;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class AlphaTests
{
    [Test] public async Task AutoSizedInteractiveBackgroundKeepsAQuietSizingCopy()
    {
        const string xml = """
            <stack id="player" padding="6%" gap="4%" background="#1a1a1a" justify="center">
              <stack id="controls" direction="horizontal" mainSize="auto" gap="3%">
                <stack id="previous" interactive="true" background="#335577" fill="true" padding="5%"><text id="prevLabel" size="12%">Prev</text></stack>
                <stack id="playPause" interactive="true" background="#2196f3a0" justify="center" align="center" fill="true" padding="5%">
                  <text id="playLabel" size="12%" visibleWhen="paused == 0">Play</text>
                  <text id="pauseLabel" size="12%" visibleWhen="paused == 1">Pause</text>
                </stack>
                <stack id="next" interactive="true" background="#335577" fill="true" padding="5%"><text id="nextLabel" size="12%">Next</text></stack>
              </stack>
            </stack>
            """;
        var hub = new DataHub(); var inputs = new List<ControlInput>();
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with { Layout = xml, InitialValues = "{\"paused\":0}" }, hub, onControlInput: inputs.Add);
        foreach (var paused in new[] { 0, 1 })
        {
            hub.Update("demo", DataHub.ParseValues("{\"paused\":" + paused + "}")); session.Refresh();
            var nodes = ButtonTests.Nodes(session.BuildTree().Root).ToArray();
            var target = nodes.Single(n => n.Id.EndsWith(".playPause"));
            var sizingBackground = target.Children[0];
            Assert.That(sizingBackground.Type, Is.EqualTo("ui.modifier"), "A direct modifier remains relative in beta.11 and establishes the auto height.");
            Assert.That(Opacity(sizingBackground), Is.EqualTo(160 / 255d));
            var sizingStack = sizingBackground.Children.Single();
            Assert.That(sizingStack.Properties["background"].GetString(), Is.EqualTo("#2196f3"));
            Assert.That(sizingStack.Children, Has.Count.EqualTo(1));
            Assert.That(Opacity(sizingStack.Children.Single()), Is.Zero);
            foreach (var node in ButtonTests.Nodes(sizingBackground))
                session.Dispatch(new() { NodeId = node.Id, Name = "press" });
            Assert.That(inputs, Is.Empty, "Measurement copies cannot publish input events.");
            session.Dispatch(new() { NodeId = target.Id, Name = "press" });
            Assert.That(inputs.Single().ElementId, Is.EqualTo("playPause"));
            inputs.Clear();
            Assert.That(ButtonTests.Text(session.BuildTree(), paused == 0 ? ".playLabel" : ".pauseLabel"), Is.EqualTo(paused == 0 ? "Play" : "Pause"));
        }
    }
    [TestCase("#f008", "#ff0000", 136)][TestCase("#12345680", "#123456", 128)]
    [TestCase("#abc", "#aabbcc", 255)][TestCase("transparent", "#000000", 0)]
    public void CssColourOrderIsUnambiguous(string value, string rgb, byte alpha)
    { Assert.That(DisplayColor.Parse(value), Is.EqualTo(new DisplayColor(rgb, alpha))); }
    [TestCase("#12345")][TestCase("rgba(1,2,3,0.5)")]
    public void MalformedColoursAreRejected(string value) => Assert.Throws<FormatException>(() => DisplayColor.Parse(value));
    private static double Opacity(UiNode node) => node.Properties["opacity"].GetDouble();
    [Test] public async Task BackgroundAndTextHaveIndependentAlphaAndKeepContentLayout()
    {
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with { Layout = "<stack id='box' padding='10%' background='#12345680'><text id='label' color='#ffffff40'>CPU</text></stack>" }, null);
        var nodes = ButtonTests.Nodes(session.BuildTree().Root).ToArray();
        Assert.That(nodes.Any(n => n.Id.EndsWith(".message")), Is.False, System.Text.Json.JsonSerializer.Serialize(session.BuildTree()));
        var background = nodes.Single(n => n.Id.EndsWith(".background"));
        Assert.That(Opacity(background), Is.EqualTo(128 / 255d));
        Assert.That(background.Children.Single().Properties["background"].GetString(), Is.EqualTo("#123456"));
        var label = nodes.Single(n => n.Id.EndsWith(".label"));
        Assert.That(Opacity(label), Is.EqualTo(64 / 255d));
        Assert.That(label.Children.Single().Properties["text"].GetString(), Is.EqualTo("CPU"));
        Assert.That(nodes.Single(n => n.Id.EndsWith(".content")).Properties["padding"].GetProperty("basis").GetDouble(), Is.EqualTo(.1));
    }
    [Test] public async Task ShapeFillAndStrokeUseIndependentAlpha()
    {
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with { Layout = "<circle id='dot' width='40%' height='40%' color='#ff000040' strokeColor='#00ff0080' strokeWidth='2%'/>" }, null);
        var nodes = ButtonTests.Nodes(session.BuildTree().Root).ToArray();
        Assert.That(nodes.Where(n => n.Type == "ui.modifier").Select(Opacity), Is.EquivalentTo(new[] { 64 / 255d, 128 / 255d }));
        var shapes = nodes.Where(n => n.Type == "ui.shape").ToArray();
        Assert.That(shapes, Has.Length.EqualTo(2));
        Assert.That(shapes.Count(n => n.Properties.ContainsKey("color")), Is.EqualTo(1));
        Assert.That(shapes.Count(n => n.Properties.ContainsKey("strokeColor")), Is.EqualTo(1));
    }
    [Test] public async Task BarInheritsStartAlphaWhenEndIsOmittedAndSupportsConditions()
    {
        var hub = new DataHub();
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with { Layout = "<bar id='bar' value='40' color='#ff000080'><style when='value > 50' color='#00ff0040'/></bar>" }, hub);
        Assert.That(Opacity(session.BuildTree().Root.Children.Single()), Is.EqualTo(128 / 255d));
        hub.Update("demo", DataHub.ParseValues("{\"value\":60}")); session.Refresh();
        Assert.That(Opacity(session.BuildTree().Root.Children.Single()), Is.EqualTo(64 / 255d));
    }
    [Test] public async Task StaticBorderOpacityDoesNotFadeChildren()
    {
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with { Layout = "<stack id='frame' borderColor='#ff000080'><text id='label'>CPU</text></stack>" }, null);
        var nodes = ButtonTests.Nodes(session.BuildTree().Root).ToArray();
        Assert.That(nodes.Count(n => n.Type == "ui.modifier"), Is.EqualTo(1));
        Assert.That(nodes.Single(n => n.Id.EndsWith(".border.opacity")).Type, Is.EqualTo("ui.modifier"));
        Assert.That(nodes.Single(n => n.Id.EndsWith(".label")).Type, Is.EqualTo("ui.text"));
    }
    [TestCase("<bar id='b' color='#ffffff80' endColor='#ff000040'/>")]
    [TestCase("<stack id='b' borderColor='#ffffff80' borderStyle='breathing'/>")]
    public void UnsupportedIndependentAlphaHasAnExplicitError(string layout)
        => Assert.Throws<FormatException>(() => new LayoutRenderer(layout).Render(DataHub.ParseValues("{}")));

    [TestCase("<circle id='dot' color='#ff000040' strokeColor='#ffffff80'/>")]
    [TestCase("<layer id='canvas'><circle id='dot' color='#ff000080' opacity='0.5'/></layer>")]
    [TestCase("<stack id='box' background='#ff000080' borderColor='#ffffff80'><text id='t'>Opaque</text></stack>")]
    public async Task LayeredOpacityUsesOverlayFramesInsteadOfRelativePositionedModifiers(string xml)
    {
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with { Layout = xml }, null);
        var nodes = ButtonTests.Nodes(session.BuildTree().Root).ToArray();
        Assert.That(nodes.Any(n => n.Id.EndsWith(".message")), Is.False);
        var layers = nodes.Where(n => n.Type == "ui.layer").ToArray();
        Assert.That(layers, Is.Not.Empty);
        Assert.That(layers.SelectMany(n => n.Children).All(n => n.Type != "ui.modifier"), Is.True,
            "The host CSS makes direct modifier children relatively positioned; frame them in a stack.");
        foreach (var frame in layers.SelectMany(n => n.Children).Where(n => n.Children.Any(c => c.Type == "ui.modifier")))
        {
            Assert.That(frame.Type, Is.EqualTo("ui.stack"));
            Assert.That(frame.Children.Single().Properties.ContainsKey("mainSize"), Is.False);
            Assert.That(frame.Children.Single().Properties["fill"].GetBoolean(), Is.True);
        }
    }
}


