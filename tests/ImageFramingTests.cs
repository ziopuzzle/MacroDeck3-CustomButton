using MacroDeck.Ui.Runtime;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class ImageFramingTests
{
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

