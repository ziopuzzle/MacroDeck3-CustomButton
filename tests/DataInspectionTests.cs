using System.Text.Json;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class DataInspectionTests
{
    [TestCase("\"N/A\"", "string", false, false, "7")]
    [TestCase("\"12.5\"", "string", false, true, "12.5")]
    [TestCase("12.5", "number", true, true, "12.5")]
    [TestCase("null", "null", false, false, "7")]
    [TestCase("true", "boolean", false, false, "7")]
    [TestCase("[]", "array", false, false, "7")]
    [TestCase("{}", "object", false, false, "7")]
    [TestCase("\"NaN\"", "string", false, false, "7")]
    public void TypeAndNumericFallbackAreConsistent(string raw, string type, bool number, bool numeric, string result)
    {
        var values = new Dictionary<string, JsonElement> { ["v"] = JsonSerializer.Deserialize<JsonElement>(raw) };
        Assert.That(DisplayCondition.Evaluate($"type(v) == '{type}'", values), Is.True);
        Assert.That(DisplayCondition.Evaluate("isNumber(v)", values), Is.EqualTo(number));
        Assert.That(DisplayCondition.Evaluate("isNumeric(v)", values), Is.EqualTo(numeric));
        Assert.That(DisplayMath.Evaluate("number(v, 7)", values), Is.EqualTo(result));
        Assert.That(DisplayCondition.Evaluate("number(v, 7) == " + result, values), Is.True);
        Assert.That(DisplayMath.Evaluate("isNumeric(v)", values), Is.EqualTo(numeric ? "1" : "0"));
    }
    [Test] public void MissingFallbackDoesNotContaminateOtherOperands()
    {
        var values = DataHub.ParseValues("{\"valid\":4}");
        Assert.That(DisplayMath.Evaluate("number(absent, 2) + valid", values), Is.EqualTo("6"));
        Assert.That(DisplayMath.Evaluate("number(valid, absent)", values), Is.EqualTo("4"));
        Assert.That(DisplayMath.Evaluate("absent + number(valid, 2)", values), Is.EqualTo("—"));
        Assert.That(DisplayCondition.Evaluate("type(absent) == 'missing' and not isNumeric(absent)", values), Is.True);
    }
    [TestCase("slider", "key='v'")][TestCase("slider", "value='{{v}}'")]
    [TestCase("dial", "key='v'")]
    public async Task InvalidControlDataUsesFallbackWithoutOverwritingIt(string type, string binding)
    {
        var hub = new DataHub(); hub.Update("demo", DataHub.ParseValues("{\"v\":\"N/A\"}"));
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with {
            Layout = $"<{type} id='control' {binding} min='0' max='100' fallback='25'/>", InitialValues = "{}" }, hub);
        var node = ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Type == "ui." + type);
        Assert.That(node.Properties["level"].GetDouble(), Is.EqualTo(.25));
        Assert.That(hub.Snapshot("demo").Values["v"].GetString(), Is.EqualTo("N/A"));
    }
    [Test] public async Task InspectorShowsEffectiveTypesAndRefreshes()
    {
        var hub = new DataHub(); hub.Update("demo", DataHub.ParseValues("{\"v\":\"N/A\"}"));
        await using var session = new ConfigurationSession(ButtonTests.Surface("config"), TestLayouts.Sample with {
            Layout = "<stack id='root'/>", InitialValues = "{\"v\":10,\"other\":true}" }, hub: hub);
        string Rows() => string.Join("\n", ButtonTests.Nodes(session.BuildTree().Root).Where(n => n.Type == "prose" && n.Id.Contains(".data"))
            .Select(n => LocalizationTests.Resolve(n.Properties["text"])));
        Assert.That(Rows(), Does.Contain("v [string, live] = \"N/A\"").And.Contain("other [boolean, initial] = true"));
        hub.Update("demo", DataHub.ParseValues("{\"v\":30}"));
        session.Dispatch(new() { NodeId = ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id.EndsWith(".refreshDiagnostics")).Id, Name = "activate" });
        Assert.That(Rows(), Does.Contain("v [number, live] = 30").And.Not.Contain("N/A"));
    }

    [TestCase("N/A")][TestCase("{{v}}bad{{v}}")][TestCase("{{= unknownFunction(v)}}")]
    public async Task FallbackDoesNotHideConfigurationMistakes(string value)
    {
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with {
            Layout = $"<slider id='control' value='{value}' fallback='25'/>", InitialValues = "{\"v\":5}" }, null);
        Assert.That(ButtonTests.Nodes(session.BuildTree().Root).Any(n => n.Id.EndsWith(".errorTitle")), Is.True);
    }
}
