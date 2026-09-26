using System.Text.Json;
using System.Text.Json.Nodes;
using MacroDeck.Ui.Model.Events;
using MacroDeck.Ui.Config;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class EventShortcutTests
{
    private const string Layout = "<stack id='panel'><stack id='play' interactive='true'><text id='label'>Play</text></stack><slider id='volume' interactive='true' key='volume'/><stack id='conditional'><style when='value > 0' interactive='true'/></stack></stack>";
    [Test] public async Task DropdownScopesEventsAndSavesConcreteTargets()
    {
        await using var config = new ConfigurationSession(ButtonTests.Surface("config"), TestLayouts.Sample with { Layout = Layout });
        JsonElement Property(string id, string property) => ButtonTests.Nodes(config.BuildTree().Root).Single(n => n.Id == id).Properties[property];
        void Select(string id, string value) => config.Dispatch(new UiEvent { NodeId = id, Name = "change", Data = JsonSerializer.SerializeToElement(value) });
        string[] Options(string id) => Property(id, "options").EnumerateArray().Select(o => o.GetProperty("value").GetString()!).ToArray();
        Assert.That(Options("inputTarget"), Is.EqualTo(new[] { "", "play", "volume", "conditional", "$other" }));
        Assert.That(Options("addInputEvent"), Does.Contain("short-press").And.Not.Contain("element-change"));
        Select("addInputEvent", "short-press");
        Select("inputTarget", "play");
        Assert.That(Options("addInputEvent"), Does.Contain("element-long-press").And.Not.Contain("element-change"));
        Select("addInputEvent", "element-press");
        Select("addInputEvent", "element-press"); // Re-selecting does not duplicate the flow.
        Assert.That(Property("flows", "value").GetArrayLength(), Is.EqualTo(2));
        Assert.That(Property("addInputEvent", "value").GetString(), Is.Empty);
        Select("inputTarget", "volume");
        Assert.That(Options("addInputEvent"), Is.EqualTo(new[] { "", "element-adjust", "element-change" }));
        Select("addInputEvent", "element-change");
        var saved = Property("flows", "value");
        Assert.That(saved.GetArrayLength(), Is.EqualTo(3));
        foreach (var flow in saved.EnumerateArray()) Assert.That(flow.GetProperty("event").GetProperty("parameters")[0].GetProperty("value").GetString(), Is.EqualTo("test-widget"));
        Assert.That(saved[2].GetProperty("event").GetProperty("parameters")[1].GetProperty("value").GetString(), Is.EqualTo("volume"));
        Select("layout", "<text id='plain'>plain</text>");
        Assert.That(Property("inputTarget", "value").GetString(), Is.Empty);
        Assert.That(Options("inputTarget"), Is.EqualTo(new[] { "", "$other" }));
        Assert.That(Property("flows", "value").GetRawText(), Is.EqualTo(saved.GetRawText()));
        foreach (var id in new[] { "inputTarget", "addInputEvent" }) Assert.That(Property(id, "transient").GetBoolean(), Is.True);
    }

    [Test] public async Task VariableShortcutCreatesNativeEditableParametersWithoutValueFilters()
    {
        await using var config = new ConfigurationSession(ButtonTests.Surface("config"), TestLayouts.Sample);
        config.Dispatch(new UiEvent { NodeId = "inputTarget", Name = "change", Data = JsonSerializer.SerializeToElement("$other") });
        config.Dispatch(new UiEvent { NodeId = "addInputEvent", Name = "change", Data = JsonSerializer.SerializeToElement("variable-changed") });
        var saved = ButtonTests.Nodes(config.BuildTree().Root).Single(n => n.Id == "flows").Properties["value"];
        var binding = saved[0].GetProperty("event");
        Assert.That(binding.GetProperty("providerId").GetString(), Is.EqualTo("macro-deck"));
        Assert.That(binding.GetProperty("eventId").GetString(), Is.EqualTo("variable-changed"));
        var parameters = binding.GetProperty("parameters");
        Assert.That(parameters[0].GetProperty("optionsSourceId").GetString(), Is.EqualTo("macrodeck.variables"));
        Assert.That(parameters[0].GetProperty("required").GetBoolean(), Is.True);
        Assert.That(parameters.EnumerateArray().All(p => p.GetProperty("value").ValueKind == JsonValueKind.Null), Is.True);
        var editable = JsonNode.Parse(saved.GetRawText())!;
        editable[0]!["event"]!["parameters"]![0]!["value"] = "test1";
        editable[0]!["children"]!.AsArray().Add(new JsonObject { ["type"] = "action", ["actionId"] = "keep" });
        var existing = JsonSerializer.SerializeToElement(editable);
        var next = ButtonEvents.AddVariableChanged(existing, "test-widget");
        Assert.That(next.GetArrayLength(), Is.EqualTo(2));
        Assert.That(next[0].GetProperty("event").GetProperty("parameters").GetRawText(), Is.EqualTo(existing[0].GetProperty("event").GetProperty("parameters").GetRawText()));
        Assert.That(ButtonEvents.AddVariableChanged(next, "test-widget").GetRawText(), Is.EqualTo(next.GetRawText()));
    }

    [Test] public void DuplicateComparisonPreservesActionsAndDistinctConditions()
    {
        var flows = ButtonEvents.AddElementEvent(ButtonEvents.Empty, "widget", "element-press", "play", Layout);
        var edited = JsonNode.Parse(flows.GetRawText())!;
        foreach (var p in edited[0]!["event"]!["parameters"]!.AsArray()) p!.AsObject().Remove("operator");
        edited[0]!["children"]!.AsArray().Add(new JsonObject { ["actionId"] = "keep" });
        var existing = JsonSerializer.SerializeToElement(edited);
        Assert.That(ButtonEvents.AddElementEvent(existing, "widget", "element-press", "play", Layout).GetRawText(), Is.EqualTo(existing.GetRawText()));
        edited[0]!["event"]!["parameters"]![1]!["operator"] = "!=";
        Assert.That(ButtonEvents.AddElementEvent(JsonSerializer.SerializeToElement(edited), "widget", "element-press", "play", Layout).GetArrayLength(), Is.EqualTo(2));
        Assert.Throws<FormatException>(() => ButtonEvents.AddElementEvent(flows, "widget", "element-change", "play", Layout));
        Assert.Throws<FormatException>(() => ButtonEvents.AddElementEvent(flows, "widget", "element-press", "label", Layout));
        var full = JsonSerializer.SerializeToElement(Enumerable.Repeat(new { triggerType = "onEvent" }, 64));
        Assert.Throws<FormatException>(() => ButtonEvents.AddVariableChanged(full, "widget"));
    }
}


