using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;
public class DisplayMathTests
{
    [TestCase("floor(-1.2)", "-2")][TestCase("ceil(-1.2)", "-1")]
    [TestCase("trunc(-1.9)", "-1")][TestCase("round(2.5)", "3")]
    [TestCase("round(-2.5)", "-3")][TestCase("round(1.234, 2)", "1.23")]
    [TestCase("abs(-2) + sqrt(9)", "5")][TestCase("pow(2, 3)", "8")]
    [TestCase("lerp(10, 20, 0.25)", "12.5")]
    [TestCase("sin(0) + cos(0)", "1")][TestCase("1e3 + 2.5E-1", "1000.25")]
    [TestCase("10 + 8 % 3 * 2", "14")][TestCase("-8 % 3", "-2")]
    public void ExpandedFunctions(string input, string expected)
        => Assert.That(DisplayMath.Evaluate(input, DataHub.ParseValues("{}")), Is.EqualTo(expected));
    [TestCase("round(1, 7)")][TestCase("round(1, 0.5)")][TestCase("floor()")]
    [TestCase("sqrt(-1)")][TestCase("pow(1e300, 2)")][TestCase("10 % 0")]
    [TestCase("1e999")][TestCase("1e+")][TestCase("min(1,2,3,4)")]
    public void ExpandedFunctionsRejectInvalidInput(string input)
        => Assert.Throws<FormatException>(() => DisplayMath.Evaluate(input, DataHub.ParseValues("{}")));
    [TestCase("{{= value / total * 100}}%", "25%")]
    [TestCase("{{= clamp(value / total, 0, 1) * 0.8}}", "0.2")]
    [TestCase("{{= (2 + 3) * -4}}", "-20")]
    [TestCase("{{= min(value, 10) + max(2, 5)}}", "15")]
    [TestCase("{{= missing / total}}", "—")]
    [TestCase("{{value:F1}}", "50.0")]
    public void ExpandsArithmetic(string input, string expected) => Assert.That(LayoutRenderer.Expand(input, DataHub.ParseValues("{\"value\":50,\"total\":200}")), Is.EqualTo(expected));
    [TestCase("1 / 0")][TestCase("clamp(1, 4, 2)")][TestCase("1 +")][TestCase("unknown(1, 2)")][TestCase("value.foo()")]
    public void RejectsInvalidExpressions(string input) => Assert.Throws<FormatException>(() => DisplayMath.Evaluate(input, DataHub.ParseValues("{}")));
    [Test] public void BoundsRecursion() => Assert.Throws<FormatException>(() => DisplayMath.Evaluate(new string('(', 40) + "1" + new string(')', 40), DataHub.ParseValues("{}")));
    [Test] public void CalculatedGeometryRenders() => Assert.DoesNotThrow(() => new LayoutRenderer("<line id='x' length='{{= clamp(value / total, 0, 1) * 80}}%'/>").Render(DataHub.ParseValues("{\"value\":1500,\"total\":2000}")));
}

