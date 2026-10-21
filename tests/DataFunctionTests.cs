using System.Text.Json;
using MacroDeck.Ui.Runtime;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class DataFunctionTests
{
    private static readonly Dictionary<string, JsonElement> Values = DataHub.ParseValues("""{"title":"  Song:001  ","numericText":"001","rows":[1,"two",true,null],"emoji":"A👨‍👩‍👧‍👦がB"}""");
    [TestCase("length(rows)", "4")]
    [TestCase("length(numericText)", "3")]
    [TestCase("length(emoji)", "4")]
    [TestCase("substring(emoji, 1, 2)", "👨‍👩‍👧‍👦が")]
    [TestCase("substring('abc', 1)", "bc")]
    [TestCase("substring('abc', 99, 9)", "")]
    [TestCase("contains(title, 'Song')", "true")]
    [TestCase("contains(title, 'song')", "false")]
    [TestCase("startsWith(trim(title), 'Song')", "true")]
    [TestCase("endsWith(trim(title), '001')", "true")]
    [TestCase("replace(trim(title), ':', '-')", "Song-001")]
    [TestCase("upper(lower('Song'))", "SONG")]
    [TestCase("join(rows, ':')", "1:two:true:")]
    [TestCase("join(split('a,,b', ','), '|')", "a||b")]
    [TestCase("at(rows, 0) + 10", "11")]
    [TestCase("at(rows, 1)", "two")]
    [TestCase("at(rows, 50)", "—")]
    [TestCase("join(slice(rows, 1, 2), '-')", "two-true")]
    [TestCase("join(slice(rows, 9), ',')", "")]
    [TestCase("length(missing)", "—")]
    public void TypedFunctions(string expression, string expected)
        => Assert.That(DisplayMath.Evaluate(expression, Values), Is.EqualTo(expected));

    [TestCase("split('x', '')")]
    [TestCase("replace('x', '', 'a')")]
    [TestCase("substring('a', -1)")]
    [TestCase("at(rows, 0.5)")]
    [TestCase("join(title, ',')")]
    [TestCase("contains(123, '2')")]
    [TestCase("length(title, 1)")]
    [TestCase("length(pow(1e300, 2))")]
    public void InvalidArgumentsFail(string expression)
        => Assert.Throws<FormatException>(() => DisplayMath.Evaluate(expression, Values));

    [Test]
    public void ConditionsAndBindingDelimitersWork()
    {
        Assert.That(DisplayCondition.Evaluate("contains(lower(title), 'song') and length(rows) == 4", Values), Is.True);
        Assert.That(DisplayCondition.Evaluate("at(rows, 1) == 'two'", Values), Is.True);
        Assert.That(LayoutRenderer.Expand("{{= replace(title, ':', '/')}}", Values), Is.EqualTo("  Song/001  "));
        Assert.That(LayoutRenderer.Expand("{{= replace('a', 'a', '{{title}}')}}", Values), Is.EqualTo("{{title}}"));
        Assert.That(LayoutRenderer.Expand("{{= at(rows, 0):F2}}", Values), Is.EqualTo("1.00"));
    }
    [Test]
    public void RepeatConsumesTypedFunctionResults()
    {
        var renderer = new LayoutRenderer("""<repeat id="r" items="{{= split('a:b:c', ':') }}"><text id="t">{{item}}</text></repeat>""");
        var root = new UiView(ButtonTests.Surface(), renderer.Render(Values)).Tree.Root;
        Assert.That(ButtonTests.Nodes(root).Where(n => n.Type == "ui.text").Select(n => n.Properties["text"].GetString()), Is.EqualTo(new[] { "a", "b", "c" }));
        Assert.That(DisplayMath.EvaluateValue("at(rows, 2)", Values).ValueKind, Is.EqualTo(JsonValueKind.True));
    }
    [Test]
    public void OutputsAreBounded()
    {
        var values = DataHub.ParseValues(JsonSerializer.Serialize(new { text = new string('a', 8000) }));
        Assert.Throws<FormatException>(() => DisplayMath.Evaluate("replace(text, 'a', 'bbbb')", values));
        Assert.Throws<FormatException>(() => DisplayMath.Evaluate("split(text, 'a')", values));
    }
}
