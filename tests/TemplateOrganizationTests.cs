using System.Text.Json;
using System.Text.Json.Nodes;
using MacroDeck.Ui.Model.Events;
using MacroDeck.Ui.Runtime;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class TemplateOrganizationTests
{
    [Test] public void NewWidgetIsEmptyAndTemplatesExactlyMatchEmbeddedExamples()
    {
        var settings = ButtonSettings.Read(JsonSerializer.Deserialize<JsonElement>(ButtonSettings.DefaultData));
        Assert.That(settings.Layout, Is.EqualTo("<stack id=\"container\"></stack>"));
        Assert.That(settings.InitialValues, Is.EqualTo("{}"));
        var assembly = typeof(LayoutTemplates).Assembly;
        var files = assembly.GetManifestResourceNames().Where(n => n.StartsWith("Templates.") && n.EndsWith(".xml")).ToArray();
        Assert.That(LayoutTemplates.All.Select(t => "Templates." + t.Id + ".xml"), Is.EquivalentTo(files));
        foreach (var template in LayoutTemplates.All)
        {
            using var reader = new StreamReader(assembly.GetManifestResourceStream("Templates." + template.Id + ".xml")!);
            Assert.That(template.Xml, Is.EqualTo(reader.ReadToEnd().Trim()));
            Assert.That(template.Category, Is.EqualTo(template.Id.StartsWith("basic_") ? "BASIC" : "ADVANCED"));
            using var data = assembly.GetManifestResourceStream("Templates." + template.Id + ".json");
            using var dataReader = data == null ? null : new StreamReader(data);
            Assert.That(template.InitialValues, Is.EqualTo(dataReader?.ReadToEnd().Trim() ?? "{}"));
        }
    }
    [TestCase(false)][TestCase(true)] public void PlayerTemplateRendersPlaybackStates(bool playing)
    {
        var template = LayoutTemplates.Get("widget_nowplaying_player");
        var data = JsonNode.Parse(template.InitialValues)!;
        data["connected"] = true; data["playing"] = playing; data["duration"] = 185; data["position"] = 62;
        var tree = new UiView(ButtonTests.Surface(), new LayoutRenderer(template.Xml).Render(DataHub.ParseValues(data.ToJsonString()))).Tree;
        Assert.That(ButtonTests.Text(tree, ".duration_mmss"), Is.EqualTo("1:02 / 3:05"));
    }
    [TestCase(false, false)][TestCase(true, false)][TestCase(true, true)]
    public async Task PlayerTemplateSessionRendersConnectionStatesWithinTransportLimits(bool connected, bool showVolume)
    {
        var template = LayoutTemplates.Get("widget_nowplaying_player");
        var data = JsonNode.Parse(template.InitialValues)!;
        data["connected"] = connected; data["showVolume"] = showVolume;
        data["duration"] = 185; data["position"] = 62;
        await using var session = new ButtonSession(ButtonTests.Surface(), new("demo", template.Xml, data.ToJsonString()), null);
        var tree = session.BuildTree();
        var nodes = ButtonTests.Nodes(tree.Root).ToArray();
        Assert.That(nodes.Any(n => n.Id.EndsWith(".message")), Is.False);
        Assert.DoesNotThrow(() => MacroDeck.Ui.Model.Serialization.UiCanonicalJson.Serialize(tree));
        Assert.That(nodes.Any(n => n.Id.EndsWith(".title")), Is.EqualTo(connected));
        Assert.That(nodes.Any(n => n.Id.EndsWith(".notConnected_text1")), Is.EqualTo(!connected));
    }
    [Test] public async Task ReorderPreservesConditionsActionsAndDescriptiveEventNames()
    {
        var source = JsonNode.Parse("""
            [{"triggerId":"one","triggerType":"onEvent","event":{"providerId":"macro-deck","eventId":"variable-changed","eventName":"Variable Changed","parameters":[{"name":"variable","value":"track_title"}]},"children":[{"actionId":"keep","value":"original"}]},
             {"triggerId":"two","triggerType":"onEvent","event":{"providerId":"net.ziopuzzle.custombutton","eventId":"display-activated","parameters":[{"name":"widgetId","value":"test-widget"}]},"children":[]}]
            """)!;
        await using var session = new ConfigurationSession(ButtonTests.Surface("config"), ButtonSettings.Default with { Flows = JsonSerializer.SerializeToElement(source) });
        JsonElement Flows() => ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id == "flows").Properties["value"];
        var before = Flows();
        Assert.That(before[0].GetProperty("event").GetProperty("eventName").GetString(), Is.EqualTo("Variable Changed — track_title"));
        session.Dispatch(new UiEvent { NodeId = "eventOrderTarget", Name = "change", Data = JsonSerializer.SerializeToElement("two") });
        session.Dispatch(new UiEvent { NodeId = ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id.EndsWith("moveEventUp")).Id, Name = "activate" });
        var after = Flows();
        Assert.That(after[0].GetRawText(), Is.EqualTo(before[1].GetRawText()));
        Assert.That(after[1].GetRawText(), Is.EqualTo(before[0].GetRawText()));
        session.Dispatch(new UiEvent { NodeId = ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id.EndsWith("moveEventUp")).Id, Name = "activate" });
        Assert.That(Flows().GetRawText(), Is.EqualTo(after.GetRawText()));
        session.Dispatch(new UiEvent { NodeId = ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id.EndsWith("moveEventDown")).Id, Name = "activate" });
        Assert.That(Flows().GetRawText(), Is.EqualTo(before.GetRawText()));
        Assert.That(ButtonEvents.MoveFlow(before, "deleted", 1).GetRawText(), Is.EqualTo(before.GetRawText()));
        var updated = JsonNode.Parse(before.GetRawText())!; updated[0]!["event"]!["parameters"]![0]!["value"] = "artist";
        session.Dispatch(new UiEvent { NodeId = "flows", Name = "change", Data = JsonSerializer.SerializeToElement(updated) });
        Assert.That(Flows()[0].GetProperty("event").GetProperty("eventName").GetString(), Is.EqualTo("Variable Changed — artist"));
    }
}


