using NUnit.Framework;
using MacroDeck.Ui.Runtime;

namespace Ziopuzzle.CustomButton.Tests;

public class ColorFunctionTests
{
    [TestCase("mixColor('#ff0000', '#00ff00', .5)", "#bcbc00")]
    [TestCase("mixColor('#ff0000', '#00ff00', .5, 'srgb')", "#808000")]
    [TestCase("mixColor('#ff0000', '#00ff00', .5, 'hsv')", "#ffff00")]
    [TestCase("mixColor('#ff0000', '#00ff00', .5, 'hsv+')", "#ffff00")]
    [TestCase("mixColor('#ff0000', '#00ff00', .5, 'hsv-increasing')", "#ffff00")]
    [TestCase("mixColor('#ff0000', '#00ff00', .5, 'hsv-')", "#0000ff")]
    [TestCase("mixColor('#ff0000', '#00ff00', .5, 'hsv-decreasing')", "#0000ff")]
    [TestCase("mixColor('#00ff00', '#ff0000', .5, 'hsv+')", "#0000ff")]
    [TestCase("mixColor('#ff0000', 'transparent', .5)", "#ff000080")]
    [TestCase("mixColor('transparent', '#00ff00', .5, 'hsv')", "#00ff0080")]
    [TestCase("mixColor('#ff000000', '#0000ff00', .5)", "#00000000")]
    [TestCase("mixColor('#ff000080', '#0000ff', .5, 'srgb')", "#5500aac0")]
    [TestCase("mixColor('#fff', '#000', -2)", "#ffffff")]
    [TestCase("mixColor('#fff', '#000', 2)", "#000000")]
    [TestCase("shiftHue('#ff000080', 120)", "#00ff0080")]
    [TestCase("shiftHue('#f00', -120)", "#0000ff")]
    [TestCase("shiftHue('#f00', 480)", "#00ff00")]
    [TestCase("setSaturation('#ff000080', 0)", "#ffffff80")]
    [TestCase("scaleSaturation('#f00', .5)", "#ff8080")]
    [TestCase("setBrightness('#f00', .5)", "#800000")]
    [TestCase("scaleBrightness('#800000', .5)", "#400000")]
    [TestCase("setBrightness('#000', 1)", "#ffffff")]
    [TestCase("scaleBrightness('#000', 5)", "#000000")]
    [TestCase("setAlpha('#1234', .5)", "#11223380")]
    [TestCase("setAlpha('#1234', 8)", "#112233")]
    [TestCase("setAlpha('#1234', -8)", "#11223300")]
    [TestCase("setAlpha(scaleSaturation('#f00', .5), .25)", "#ff808040")]
    [TestCase("setAlpha(color, clamp(amount / 100, 0, 1))", "#ff000080")]
    [TestCase("mixColor(color, '#000', number(absent, .5), mode)", "#800000")]
    [TestCase("mixColor(missing, '#000', .5)", "—")]
    public void Functions(string expression, string expected)
        => Assert.That(DisplayMath.Evaluate(expression, DataHub.ParseValues("{\"color\":\"#ff0000\",\"amount\":50,\"mode\":\"srgb\"}")), Is.EqualTo(expected));

    [TestCase("setAlpha('#fff', 1 / 0)")]
    [TestCase("setBrightness('#fff', sqrt(-1))")]
    [TestCase("shiftHue('N/A', 10)")]
    [TestCase("mixColor('#fff', '#000', .5, 'bad')")]
    [TestCase("mixColor('#fff', '#000')")]
    [TestCase("setAlpha('#fff', .5, .5)")]
    [TestCase("mixColor('#fff', '#000', .5, 'hsv', 5)")]
    [TestCase("setAlpha(15, .5)")]
    [TestCase("shiftHue('#f00', 'text')")]
    [TestCase("setAlpha('#fff, .5)")]
    [TestCase("setAlpha('#fff', .5) + 1")]
    [TestCase("min(setAlpha('#fff', .5), 1)")]
    public void InvalidExpressions(string expression)
        => Assert.Throws<FormatException>(() => DisplayMath.Evaluate(expression, DataHub.ParseValues("{}")));

    [Test] public void HueWrapAndAchromaticEndpointsAreStable()
    {
        var a = ColorInterpolation.Encode([350, 1, 1], "hsv");
        var b = ColorInterpolation.Encode([10, 1, 1], "hsv");
        var shortest = DisplayColor.Parse(DisplayMath.Evaluate($"mixColor('{a}', '{b}', .5, 'hsv')", DataHub.ParseValues("{}")));
        Assert.That(shortest.Hex, Is.EqualTo("#ff0000"));
        Assert.That(DisplayMath.Evaluate("mixColor('#808080', '#00ff00', .5, 'hsv')", DataHub.ParseValues("{}")), Is.EqualTo("#60c060"));
        Assert.That(DisplayMath.Evaluate("mixColor('#f00', '#f00', .5, 'hsv-')", DataHub.ParseValues("{}")), Is.EqualTo("#ff0000"));
    }
    [Test] public void RendererUsesExpressionsForBackgroundTextAndGradient()
    {
        var renderer = new LayoutRenderer("<stack id='root' background=\"{{= mixColor(color, '#000', .5, 'srgb')}}\"><text id='label' color=\"{{= shiftHue(color, 120)}}\">{{= setAlpha(color, .5)}}</text><rect id='box' color=\"{{= setBrightness(color, .5)}}\" endColor=\"{{= shiftHue(color, -120)}}\" gradient='linear'/></stack>");
        var tree = new UiView(ButtonTests.Surface(), renderer.Render(DataHub.ParseValues("{\"color\":\"#ff0000\"}"))).Tree;
        Assert.That(tree.Root.Properties["background"].GetString(), Is.EqualTo("#800000"));
        Assert.That(ButtonTests.Text(tree, ".label"), Is.EqualTo("#ff000080"));
    }
    [Test] public void ColorNestingKeepsExpressionLimits()
    {
        Assert.Throws<FormatException>(() => DisplayMath.Evaluate(new string('(', 33) + "'#fff'" + new string(')', 33), DataHub.ParseValues("{}")));
        Assert.Throws<FormatException>(() => DisplayMath.Evaluate("'" + new string('a', 257) + "'", DataHub.ParseValues("{}")));
    }
}
