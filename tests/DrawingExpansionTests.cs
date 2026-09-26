using MacroDeck.Ui.Model.Nodes;
using MacroDeck.Ui.Runtime;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class DrawingExpansionTests
{
    private sealed class Clock : TimeProvider
    {
        public long Milliseconds;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => Milliseconds;
    }
    private static UiNode Render(string xml, string json = "{}") => new UiView(ButtonTests.Surface(), new LayoutRenderer(xml).Render(DataHub.ParseValues(json))).Tree.Root;

    [Test] public void EndpointsAndGradientAnglesInterpolateWithoutChangingIdentity()
    {
        var clock = new Clock(); var animation = new DisplayAnimation(clock);
        var renderer = new LayoutRenderer("<layer id='canvas'><line id='line' x1='10%' y1='10%' x2='{{position}}' y2='90%' transitionMs='1000' transitionProperties='x2' easing='linear'/><rect id='rect' gradient='linear' gradientAngle='{{angle}}' transitionMs='1000' transitionProperties='gradientAngle' easing='linear'/></layer>");
        UiNode Frame(string json) => new UiView(ButtonTests.Surface(), renderer.Render(DataHub.ParseValues(json), animation: animation)).Tree.Root;
        var initial = Frame("{\"position\":0.2,\"angle\":0}");
        Frame("{\"position\":0.8,\"angle\":180}"); clock.Milliseconds = 500;
        var nodes = ButtonTests.Nodes(Frame("{\"position\":0.8,\"angle\":180}")).ToArray();
        Assert.That(nodes.Single(n => n.Type == "ui.shape").Properties["path"].GetString(), Is.EqualTo("M0.1 0.1 L0.5 0.9"));
        Assert.That(nodes.Single(n => n.Properties.ContainsKey("modifiers")).Properties["modifiers"].GetProperty("background").GetProperty("linear").GetProperty("angle").GetDouble(), Is.EqualTo(90));
        Assert.That(nodes.Select(n => n.Id), Is.EqualTo(ButtonTests.Nodes(initial).Select(n => n.Id)));
    }
    [Test] public void ExistingBoldWeightCanBeConditional()
    {
        const string xml = "<text id='text'>Label<style when='active == 1' weight='bold'/></text>";
        Assert.That(Render(xml, "{\"active\":1}").Properties["weight"].GetString(), Is.EqualTo("bold"));
        Assert.That(Render(xml).Properties["weight"].GetString(), Is.EqualTo("regular"));
    }

    [Test] public void EndpointLineUsesOpenStrokeAndBoundCoordinates()
    {
        var root = Render("<line id='line' x1='90%' y1='10%' x2='{{x}}%' y2='90%' color='#ff000080' thickness='3%'/>", "{\"x\":20}");
        var shape = ButtonTests.Nodes(root).Single(n => n.Type == "ui.shape");
        Assert.That(shape.Properties["path"].GetString(), Is.EqualTo("M0.9 0.1 L0.2 0.9"));
        Assert.That(shape.Properties.ContainsKey("color"), Is.False);
        Assert.That(shape.Properties["strokeColor"].GetString(), Is.EqualTo("#ff0000"));
        Assert.That(root.Properties["opacity"].GetDouble(), Is.EqualTo(128d / 255));
    }
    [TestCase("x1='0' y1='0' x2='0' y2='0'")]
    [TestCase("x1='0' y1='0' x2='1' y2='1' thickness='0'")]
    public void EmptyLineDoesNotDraw(string attributes) => Assert.That(ButtonTests.Nodes(Render($"<line id='line' {attributes}/>")).Any(n => n.Type == "ui.shape"), Is.False);

    [TestCase("x1='0'")][TestCase("x1='0' y1='0' x2='1' y2='1' direction='horizontal'")]
    [TestCase("x1='-1' y1='0' x2='1' y2='1'")]
    public void InvalidEndpointsAreExplained(string attributes) => Assert.Throws<FormatException>(() => Render($"<line id='line' {attributes}/>"));

    [TestCase("rect", "linear", "bounds")][TestCase("circle", "radial", "circle")][TestCase("capsule", "linear", "capsule")]
    public void GradientUsesNativeBackgroundAndIndependentStroke(string type, string kind, string clip)
    {
        var root = Render($"<{type} id='shape' width='40%' height='40%' color='#ff000080' endColor='#00ff0080' gradient='{kind}' gradientAngle='135' strokeColor='#ffffff' strokeWidth='1%'/>");
        var nodes = ButtonTests.Nodes(root).ToArray();
        var paint = nodes.Single(n => n.Properties.TryGetValue("clip", out var c) && c.GetString() == clip);
        var gradient = paint.Properties["modifiers"].GetProperty("background").GetProperty(kind);
        Assert.That(gradient.GetProperty("stops")[0].GetProperty("color").GetString(), Is.EqualTo("#ff0000"));
        Assert.That(gradient.GetProperty("stops")[1].GetProperty("color").GetString(), Is.EqualTo("#00ff00"));
        Assert.That(nodes.Single(n => n.Type == "ui.shape").Properties["strokeColor"].GetString(), Is.EqualTo("#ffffff"));
        Assert.That(nodes.Count(n => n.Properties.TryGetValue("opacity", out var p) && p.GetDouble() == 128d / 255), Is.EqualTo(1));
    }
    [Test] public void GradientStyleAndAlphaFallbackAreSupported()
    {
        const string xml = "<rect id='shape' color='#12345680'><style when='active == 1' gradient='linear' gradientAngle='{{angle}}'/></rect>";
        var root = Render(xml, "{\"active\":1,\"angle\":45}");
        var paint = ButtonTests.Nodes(root).Single(n => n.Properties.ContainsKey("modifiers"));
        var linear = paint.Properties["modifiers"].GetProperty("background").GetProperty("linear");
        Assert.That(linear.GetProperty("angle").GetDouble(), Is.EqualTo(45));
        Assert.That(linear.GetProperty("stops")[1].GetProperty("color").GetString(), Is.EqualTo("#123456"));
        Assert.That(ButtonTests.Nodes(Render(xml)).Any(n => n.Properties.ContainsKey("modifiers")), Is.False);
    }
    [TestCase("<rect id='r' gradient='linear' color='#ff000080' endColor='#00ff00'/>")]
    [TestCase("<circle id='r' gradient='linear' width='20%' height='40%'/>")]
    [TestCase("<rect id='r' gradient='unknown'/>")]
    public void UnsupportedGradientSettingsFailClearly(string xml) => Assert.Throws<FormatException>(() => Render(xml));

    [TestCase(true)][TestCase(false)]
    public void WrapLayerPreservesSubtreeAndUndo(bool root)
    {
        var xml = root ? "<text id='t'>Value<style when='true' color='#fff'/></text>" : "<stack id='parent'><text id='t'>Value<style when='true' color='#fff'/></text><text id='other'>Other</text></stack>";
        var history = new LayoutEditHistory(xml);
        var id = history.Edit(d => d.Wrap("t", "layer"));
        var document = new LayoutDocument(history.Xml);
        Assert.That(document.Find("t").Parent!.Name.LocalName, Is.EqualTo("layer"));
        Assert.That(document.Find("t").Element("style"), Is.Not.Null);
        Render(history.Xml);
        history.Undo(); Assert.That(new LayoutDocument(history.Xml).Find("t").Parent?.Name.LocalName, Is.EqualTo(root ? null : "stack"));
        history.Redo(); Assert.That(history.Xml, Does.Contain("<layer"));
    }
}

