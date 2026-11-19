using System.Text.Json;
using System.Text.Json.Nodes;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class EventFlowNormalizationTests
{
    [Test]
    public void DuplicateShortcutNormalizesLegacyTriggersWithoutLosingFlowOrderOrMetadata()
    {
        var original = JsonSerializer.Deserialize<JsonElement>("""
            [{"triggerId":"first","triggerType":"onShortPress","custom":{"number":1.2300,"text":"A & B"},"children":[{"id":"a","parameters":[{"value":{"expression":"{{ value }}","parts":[null,false,1e2]}}]}]},
             {"triggerId":"second","triggerType":"onEvent","event":{"providerId":"other","eventId":"custom","parameters":[]},"children":[]}]
            """);
        var expected = ButtonEvents.Normalize(original, "owner");
        var actual = ButtonEvents.AddElementEvent(original, "owner", "short-press", null, "");

        Assert.That(actual.GetRawText(), Is.EqualTo(expected.GetRawText()));
        Assert.That(actual.EnumerateArray().Select(f => f.GetProperty("triggerId").GetString()), Is.EqualTo(new[] { "first", "second" }));
        Assert.That(actual[0].GetProperty("custom").GetProperty("number").GetRawText(), Is.EqualTo("1.2300"));
        Assert.That(actual[0].GetProperty("children")[0].GetProperty("parameters")[0].GetProperty("value").GetProperty("parts")[2].GetRawText(), Is.EqualTo("1e2"));
    }

    [Test]
    public void ChannelDefaultsChangeOnlyPlaceholderStringsAndRetainOtherJsonValues()
    {
        var values = JsonNode.Parse("""[null,"","demo",0,false,{},["demo"],"Demo"," demo ","{{channel}}"]""")!.AsArray();
        var flows = new JsonArray(new JsonObject { ["triggerType"] = "onEvent", ["children"] = new JsonArray(
            values.Select((value, index) => (JsonNode)new JsonObject {
                ["id"] = "action-" + index, ["integrationId"] = CustomButtonIntegration.PluginId, ["actionId"] = "set-data",
                ["parameters"] = new JsonArray(new JsonObject { ["name"] = "channel", ["value"] = value?.DeepClone() })
            }).ToArray()) });
        var input = JsonSerializer.SerializeToElement(flows);
        var actual = ActionEditorDefaults.ApplyChannel(ButtonEvents.Empty, input, "music");
        var actions = actual[0].GetProperty("children").EnumerateArray().ToArray();

        Assert.That(actions.Take(3).Select(a => a.GetProperty("parameters")[0].GetProperty("value").GetString()), Is.EqualTo(new[] { "music", "music", "music" }));
        for (var i = 3; i < actions.Length; i++)
            Assert.That(actions[i].GetRawText(), Is.EqualTo(input[0].GetProperty("children")[i].GetRawText()));
        Assert.That(ActionEditorDefaults.ApplyChannel(input, input, "music").GetRawText(), Is.EqualTo(input.GetRawText()));
    }

    [Test]
    public void SourceFilteredRetargetPreservesActionsAndUnmatchedEventParameters()
    {
        var original = JsonSerializer.Deserialize<JsonElement>("""
            [{"triggerId":"own","triggerType":"onEvent","event":{"providerId":"net.ziopuzzle.custombutton","eventId":"short-press","parameters":[{"name":"widgetId","type":"widget-target","value":"source","valueLabel":"Source"},{"name":"elementId","value":"source","valueLabel":"Keep"}]},"children":[{"id":"keep","branches":[{"children":[{"parameters":[{"value":{"expression":"{{source}}","number":1.20}}]}]}]}]},
             {"triggerId":"external","triggerType":"onEvent","event":{"providerId":"other","eventId":"custom","parameters":[{"name":"target","type":"widget-target","value":"source","valueLabel":"Source"},{"name":"otherTarget","type":"widget-target","value":"different","valueLabel":"Different"},{"name":"literal","value":"source"}]},"children":[]}]
            """);
        var normalized = ButtonEvents.Normalize(original, "destination");
        var actual = ActionEditorDefaults.RetargetEvents(original, "destination", "source");
        var own = actual[0].GetProperty("event").GetProperty("parameters");
        var external = actual[1].GetProperty("event").GetProperty("parameters");

        Assert.That(own[0].GetProperty("value").GetString(), Is.EqualTo("$self"));
        Assert.That(own[0].GetProperty("allowSelf").GetBoolean(), Is.True);
        Assert.That(own[0].TryGetProperty("valueLabel", out _), Is.False);
        Assert.That(own[1].GetRawText(), Is.EqualTo(normalized[0].GetProperty("event").GetProperty("parameters")[1].GetRawText()));
        Assert.That(external[0].GetProperty("value").GetString(), Is.EqualTo("destination"));
        Assert.That(external[0].TryGetProperty("valueLabel", out _), Is.False);
        Assert.That(external[1].GetRawText(), Is.EqualTo(normalized[1].GetProperty("event").GetProperty("parameters")[1].GetRawText()));
        Assert.That(external[2].GetRawText(), Is.EqualTo(normalized[1].GetProperty("event").GetProperty("parameters")[2].GetRawText()));
        Assert.That(actual[0].GetProperty("children").GetRawText(), Is.EqualTo(normalized[0].GetProperty("children").GetRawText()));
        Assert.That(actual.EnumerateArray().Select(f => f.GetProperty("triggerId").GetString()), Is.EqualTo(new[] { "own", "external" }));
    }
}
