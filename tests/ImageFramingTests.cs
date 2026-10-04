using MacroDeck.Ui.Runtime;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class ImageFramingTests
{
    [TestCase("image", false)][TestCase("image", true)][TestCase("svg", false)]
    public void TintPreservesTransparentBackgroundAndMultipliesAlpha(string type, bool hostImage)
    {
        var content = type == "svg" ? "<![CDATA[<svg width='10' height='10'><rect width='10' height='10'/></svg>]]>" : "";
        var root = new UiView(ButtonTests.Surface(), new LayoutRenderer($"<{type} id='art' color='{{{{tint}}}}' opacity='0.5' fit='cover' zoom='1.2'>{content}</{type}>")
            .Render(DataHub.ParseValues("{\"tint\":\"#ff880080\"}"), imageAspectRatio: _ => hostImage ? double.NaN : 2)).Tree.Root;
        var image = ButtonTests.Nodes(root).Single(n => n.Type == "ui.button");
        Assert.That(image.Properties["tint"].GetString(), Is.EqualTo("#ff8800"));
        Assert.That(image.Properties["background"].GetString(), Is.EqualTo("transparent"));
        Assert.That(image.Properties["opacity"].GetDouble(), Is.EqualTo(.5 * 128 / 255));
        Assert.That(image.Properties["zoom"].GetDouble(), Is.EqualTo(1.2));
        Assert.That(image.Properties["fit"].GetString(), Is.EqualTo("cover"));
    }

    [Test] public void ConditionalTintCanClearWithoutChangingNodeIdentity()
    {
        var renderer = new LayoutRenderer("<image id='art'><style when='active == 1' color='#00ff00'/></image>");
        MacroDeck.Ui.Model.Nodes.UiNode Render(int active) => new UiView(ButtonTests.Surface(), renderer.Render(DataHub.ParseValues($"{{\"active\":{active}}}"))).Tree.Root;
        var tinted = Render(1); var original = Render(0);
        Assert.That(ButtonTests.Nodes(original).Select(n => n.Id), Is.EqualTo(ButtonTests.Nodes(tinted).Select(n => n.Id)));
        Assert.That(ButtonTests.Nodes(original).Single(n => n.Type == "ui.button").Properties.ContainsKey("tint"), Is.False);
    }

    [Test] public void TintSupportsColourInterpolation()
    {
        var clock = new Clock(); var animation = new DisplayAnimation(clock);
        var renderer = new LayoutRenderer("<image id='art' color='{{color}}' transitionMs='1000' transitionProperties='color' colorSpace='srgb' easing='linear'/>");
        string Tint(string color) => ButtonTests.Nodes(new UiView(ButtonTests.Surface(), renderer.Render(DataHub.ParseValues($"{{\"color\":\"{color}\"}}"), animation: animation)).Tree.Root)
            .Single(n => n.Type == "ui.button").Properties["tint"].GetString()!;
        Tint("#000000"); Tint("#ffffff"); clock.Milliseconds = 500;
        Assert.That(Tint("#ffffff"), Is.EqualTo("#808080"));
    }
    [TestCase("vertical", "start", "")]
    [TestCase("horizontal", "start", "")]
    [TestCase("vertical", "end", "zoom='1'")]
    [TestCase("horizontal", "end", "fit='cover'")]
    public void ImagesKeepANaturalSquareFootprintInStacks(string direction, string alignment, string extra)
    {
        var root = new UiView(ButtonTests.Surface(), new LayoutRenderer($"<stack id='parent' direction='{direction}' justify='{alignment}' align='{alignment}'><image id='art' size='70%' {extra}/></stack>")
            .Render(DataHub.ParseValues("{}"), imageAspectRatio: _ => double.NaN)).Tree.Root;
        var frame = root.Children.Single();
        Assert.That(frame.Type, Is.EqualTo("ui.modifier"));
        Assert.That(frame.Properties["fill"].GetBoolean(), Is.False);
        Assert.That(frame.Properties["frame"].GetProperty("width").GetProperty("basis").GetDouble(), Is.EqualTo(.7));
        Assert.That(frame.Properties["frame"].GetProperty("height").GetProperty("basis").GetDouble(), Is.EqualTo(.7));
        foreach (var button in ButtonTests.Nodes(root).Where(n => n.Type == "ui.button"))
            Assert.That(button.Properties["background"].GetString(), Is.EqualTo("transparent"));
    }

    [Test] public void LayerArtworkDoesNotIntroduceAnOpaqueBackground()
    {
        var root = new UiView(ButtonTests.Surface(), new LayoutRenderer("<layer id='root'><icon id='under' name='star'/><image id='over' size='70%' fit='cover'/></layer>")
            .Render(DataHub.ParseValues("{}"), imageAspectRatio: _ => double.NaN)).Tree.Root;
        Assert.That(ButtonTests.Nodes(root).Any(n => n.Type == "ui.icon"), Is.True);
        var button = ButtonTests.Nodes(root).Single(n => n.Type == "ui.button");
        Assert.That(button.Properties["background"].GetString(), Is.EqualTo("transparent"));
    }
    private sealed class Clock : TimeProvider
    {
        public long Milliseconds;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => Milliseconds;
    }
    [TestCase("contain", 1.5)][TestCase("cover", 3)]
    public void FramingAnimatesInTheImageBoxAndKeepsNodeIdentity(string fit, double expectedZoom)
    {
        var clock = new Clock(); var animation = new DisplayAnimation(clock);
        var renderer = new LayoutRenderer($"<image id='art' fit='{fit}' zoom='{{{{zoom}}}}' offsetX='{{{{x}}}}' offsetY='-0.2' transitionMs='1000' transitionProperties='zoom offsetX' easing='linear'/>");
        MacroDeck.Ui.Model.Nodes.UiNode Render(string json) => new UiView(ButtonTests.Surface(), renderer.Render(DataHub.ParseValues(json), animation: animation, imageAspectRatio: _ => 2)).Tree.Root;
        var initial = Render("{\"zoom\":1,\"x\":0}");
        Render("{\"zoom\":2,\"x\":0.5}");
        clock.Milliseconds = 500;
        var frame = Render("{\"zoom\":2,\"x\":0.5}");
        var transform = ButtonTests.Nodes(frame).Single(n => n.Type == "ui.transform");
        Assert.That(transform.Properties["zoom"].GetDouble(), Is.EqualTo(expectedZoom));
        Assert.That(transform.Properties["offsetX"].GetDouble(), Is.EqualTo(.25));
        Assert.That(transform.Properties["offsetY"].GetDouble(), Is.EqualTo(-.2));
        Assert.That(ButtonTests.Nodes(frame).Select(n => n.Id), Is.EqualTo(ButtonTests.Nodes(initial).Select(n => n.Id)));
        Assert.That(frame.Properties["clip"].GetString(), Is.EqualTo("bounds"));
    }
    [Test] public void ConditionalZoomCanAnimateBackToItsDefault()
    {
        var clock = new Clock(); var animation = new DisplayAnimation(clock);
        var renderer = new LayoutRenderer("<image id='art' transitionMs='1000' transitionProperties='zoom' easing='linear'><style when='active == 1' zoom='2'/></image>");
        MacroDeck.Ui.Model.Nodes.UiNode Render(string json) => new UiView(ButtonTests.Surface(), renderer.Render(DataHub.ParseValues(json), animation: animation)).Tree.Root;
        Render("{\"active\":1}"); Render("{\"active\":0}"); clock.Milliseconds = 500;
        var transform = ButtonTests.Nodes(Render("{\"active\":0}")).Single(n => n.Type == "ui.transform");
        Assert.That(transform.Properties["zoom"].GetDouble(), Is.EqualTo(1.5));
    }
    [TestCase("zoom='0'")][TestCase("zoom='5'")][TestCase("offsetX='1.01'")][TestCase("offsetY='-1.01'")]
    public void InvalidFramingHasAnActionableValidationError(string attribute)
        => Assert.Throws<FormatException>(() => new LayoutRenderer($"<image id='art' {attribute}/>").Render(DataHub.ParseValues("{}")));
}

