using System.Text.Json;
using System.Text.Json.Nodes;
using MacroDeck.Ui.Model.Events;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class ActionEditorDefaultsTests
{
    private static JsonElement Flow(string channel = "demo", string provider = CustomButtonIntegration.PluginId, string action = "set-value") => JsonSerializer.SerializeToElement(new[] {
        new { triggerType = "onEvent", children = new[] { new { id = "loop", children = new[] {
            new { id = "action-1", integrationId = provider, actionId = action, parameters = new[] { new { name = "channel", value = channel } } }
        } } }, @event = new { providerId = CustomButtonIntegration.PluginId, eventId = "element-press", parameters = new[] {
            new { name = "widgetId", value = "original" }, new { name = "elementId", value = "play" }
        } } }
    });
    private static string? Channel(JsonElement flows) => flows[0].GetProperty("children")[0].GetProperty("children")[0].GetProperty("parameters")[0].GetProperty("value").GetString();

    [TestCase("set-value")][TestCase("set-data")]
    public async Task NewNestedActionsUseCurrentChannelButLaterEditsRemainEditable(string action)
    {
        await using var session = new ConfigurationSession(ButtonTests.Surface("config"), TestLayouts.Sample with { Channel = "music", Flows = ButtonEvents.Empty });
        JsonElement Current() => ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id == "flows").Properties["value"];
        session.Dispatch(new UiEvent { NodeId = "channel", Name = "change", Data = JsonSerializer.SerializeToElement("updated") });
        session.Dispatch(new UiEvent { NodeId = "flows", Name = "change", Data = Flow(action: action) });
        Assert.That(Channel(Current()), Is.EqualTo("updated"));
        session.Dispatch(new UiEvent { NodeId = "flows", Name = "change", Data = Flow(action: action) });
        Assert.That(Channel(Current()), Is.EqualTo("demo"));
    }

    [Test]
    public void PreservesExistingAndExplicitChannelsAndOtherProviders()
    {
        Assert.That(Channel(ActionEditorDefaults.ApplyChannel(Flow(), Flow(), "music")), Is.EqualTo("demo"));
        Assert.That(Channel(ActionEditorDefaults.ApplyChannel(ButtonEvents.Empty, Flow("other"), "music")), Is.EqualTo("other"));
        Assert.That(Channel(ActionEditorDefaults.ApplyChannel(ButtonEvents.Empty, Flow(provider: "other"), "music")), Is.EqualTo("demo"));
        var branch = JsonNode.Parse(Flow().GetRawText())!;
        var container = branch[0]!["children"]![0]!.AsObject();
        var children = container["children"]!.DeepClone();
        container.Remove("children");
        container["branches"] = new JsonArray(new JsonObject { ["children"] = children });
        var result = ActionEditorDefaults.ApplyChannel(ButtonEvents.Empty, JsonSerializer.SerializeToElement(branch), "music");
        Assert.That(result[0].GetProperty("children")[0].GetProperty("branches")[0].GetProperty("children")[0].GetProperty("parameters")[0].GetProperty("value").GetString(), Is.EqualTo("music"));
    }

    [Test]
    public async Task BulkRetargetUpdatesEventsWithoutChangingActionsOrOtherConditions()
    {
        var input = JsonNode.Parse(Flow().GetRawText())!.AsArray();
        input.Add(JsonNode.Parse("""
        {"triggerType":"onEvent","event":{"providerId":"other","eventId":"example","parameters":[{"name":"target","type":"widget-target","value":"old","valueLabel":"Old"},{"name":"variable","value":"title"}]},"children":[]}
        """));
        await using var session = new ConfigurationSession(ButtonTests.Surface("config"), TestLayouts.Sample with { Flows = JsonSerializer.SerializeToElement(input) });
        session.Dispatch(new UiEvent { NodeId = ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id.EndsWith("retargetEvents")).Id, Name = "activate" });
        var result = ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id == "flows").Properties["value"];
        Assert.That(result[0].GetProperty("event").GetProperty("parameters")[0].GetProperty("value").GetString(), Is.EqualTo("test-widget"));
        Assert.That(result[0].GetProperty("event").GetProperty("parameters")[1].GetProperty("value").GetString(), Is.EqualTo("play"));
        Assert.That(Channel(result), Is.EqualTo("demo"));
        Assert.That(result[1].GetProperty("event").GetProperty("parameters")[0].GetProperty("value").GetString(), Is.EqualTo("test-widget"));
        Assert.That(result[1].GetProperty("event").GetProperty("parameters")[0].TryGetProperty("valueLabel", out _), Is.False);
        Assert.That(result[1].GetProperty("event").GetProperty("parameters")[1].GetProperty("value").GetString(), Is.EqualTo("title"));
    }
}

