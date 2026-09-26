using MacroDeck.Ui.Runtime;
using MacroDeck.Ui.Model.Nodes;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class ColorTransitionTests
{
    private sealed class Clock : TimeProvider
    {
        public long Time;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => Time;
    }
    [TestCase("srgb", "#808000")][TestCase("linear-rgb", "#bcbc00")][TestCase("hsv", "#ffff00")]
    public void RedToGreenHasSelectableMidpoint(string space, string expected)
    {
        var clock = new Clock(); var state = new DisplayAnimation(clock);
        string Render(string target) { state.BeginFrame(); var color = state.Color("color", target, 1000, "linear", space); state.EndFrame(); return color; }
        Render("#ff0000"); Render("#00ff00"); clock.Time = 500;
        Assert.That(Render("#00ff00"), Is.EqualTo(expected));
        clock.Time = 1000; Assert.That(Render("#00ff00"), Is.EqualTo("#00ff00"));
    }
    [Test] public void HsvUsesShortestHuePathAndDoesNotColourGreyTransitionsRed()
    {
        var clock = new Clock(); var state = new DisplayAnimation(clock);
        string Render(string target) { state.BeginFrame(); var c = state.Color("a", target, 1000, "linear", "hsv"); state.EndFrame(); return c; }
        Render("#0000ff"); Render("#ff0000"); clock.Time = 500;
        Assert.That(Render("#ff0000"), Is.EqualTo("#ff00ff"));
        state.Reset(); Render("#ffffff"); Render("#00ff00"); clock.Time += 500;
        Assert.That(Render("#00ff00"), Is.EqualTo("#80ff80"));
    }
    [TestCase("linear-rgb")][TestCase("hsv")][TestCase("srgb")]
    public void RepeatedDuplicateUpdatesDoNotResetOrReplaceTheButton(string space)
    {
        var clock = new Clock(); var state = new DisplayAnimation(clock);
        var renderer = new LayoutRenderer($"<stack id='content' background='{{{{color}}}}' transitionMs='1500' transitionProperties='background' easing='linear' colorSpace='{space}'/>");
        var colors = new[] { "#ff0000", "#00ff00", "#0000ff" };
        UiNode? previous = null;
        for (clock.Time = 0; clock.Time <= 30000; clock.Time += 20)
        {
            var target = colors[clock.Time / 1000 % 3];
            // Both saved event flows write the same reading; two renders at one timestamp.
            UiNode Render() => new UiView(ButtonTests.Surface(), renderer.Render(DataHub.ParseValues("{\"color\":\"" + target + "\"}"), animation: state)).Tree.Root;
            var node = Render(); var duplicate = Render();
            Assert.That(TreeChanges.Between(node, duplicate), Is.Empty);
            if (previous != null)
            {
                Assert.That(TreeChanges.Between(previous, node).All(p => p.Op == "set-properties"), Is.True);
                var a = ColorInterpolation.Decode(previous.Properties["background"].GetString()!, "srgb");
                var b = ColorInterpolation.Decode(node.Properties["background"].GetString()!, "srgb");
                Assert.That(a.Zip(b, (x, y) => Math.Abs(x - y)).Max(), Is.LessThan(.15), $"Jump at {clock.Time} ms");
            }
            previous = node;
        }
    }
    [Test] public void MultilineTextHasAnIndependentClippingFrameAndKeepsAlpha()
    {
        var renderer = new LayoutRenderer("<text id='title' size='12%' maxLines='2' color='#ffffff80'>Long track name</text>");
        var root = new UiView(ButtonTests.Surface(), renderer.Render(DataHub.ParseValues("{}"))).Tree.Root;
        Assert.That(root.Properties["clip"].GetString(), Is.EqualTo("bounds"));
        Assert.That(root.Properties["frame"].GetProperty("maxHeight").GetProperty("basis").GetDouble(), Is.EqualTo(.288));
        Assert.That(ButtonTests.Nodes(root).Any(n => n.Properties.ContainsKey("opacity")), Is.True);
        Assert.That(ButtonTests.Nodes(root).Single(n => n.Type == "ui.text").Properties["maxLines"].GetInt32(), Is.EqualTo(2));
    }
}

