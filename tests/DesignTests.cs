using System.Text.Json;
using System.Text.Json.Nodes;
using MacroDeck.Ui.Model.Events;
using MacroDeck.Ui.Model.Nodes;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class DesignTests
{
    private static JsonObject Data(string preset = "gauge")
    {
        var data = JsonNode.Parse(ButtonSettings.DefaultData)!.AsObject();
        data["designPreset"] = preset;
        return data;
    }
    private static ButtonSettings Read(JsonObject data) => ButtonSettings.Read(JsonSerializer.SerializeToElement(data));
    private static UiNode Node(UiTree tree, string suffix) => ButtonTests.Nodes(tree.Root).Single(n => n.Id.EndsWith(suffix, StringComparison.Ordinal));

    [Test] public async Task LegacyXmlIsPreservedThroughAnEditorRoundTrip()
    {
        var data = JsonSerializer.SerializeToElement(new { channel = "old", layout = "<text id=\"legacy\">Legacy</text>", initialValues = "{}" });
        var settings = ButtonSettings.Read(data);
        Assert.That(settings.Design!.Preset, Is.EqualTo("xml"));
        await using var config = new ConfigurationSession(ButtonTests.Surface("config"), settings);
        var stored = JsonNode.Parse(data.GetRawText())!.AsObject();
        foreach (var node in ButtonTests.Nodes(config.BuildTree().Root))
            if (node.Properties.TryGetValue("value", out var value)) stored[node.Id] = JsonNode.Parse(value.GetRawText());
        Assert.That(stored["layout"]!.GetValue<string>(), Is.EqualTo(settings.Layout));
        await using var button = new ButtonSession(ButtonTests.Surface(), Read(stored), null);
        Assert.That(ButtonTests.Text(button.BuildTree(), ".legacy"), Is.EqualTo("Legacy"));
    }

    [Test] public async Task LegacyFormValuesRemainCompatibleWithoutXmlOrJson()
    {
        var data = Data();
        data["layout"] = "invalid XML retained for later";
        data["initialValues"] = "invalid JSON retained for later";
        await using var config = new ConfigurationSession(ButtonTests.Surface("config"), Read(data));
        void Edit(string key, object value) => data[key] = JsonSerializer.SerializeToNode(value);
        Edit("designTitle", "日本語 <tag> & {{literal}}");
        Edit("designKey", "load");
        Edit("designFallback", "25");
        Edit("designUnit", " pts");
        Edit("designTextSize", 24d);
        Edit("designBackground", "#123456");
        Edit("designBarMax", 50d);
        var schema = JsonNode.Parse(ButtonSettings.Schema)!["properties"]!.AsObject();
        foreach (var node in ButtonTests.Nodes(config.BuildTree().Root))
        {
            if (!node.Properties.TryGetValue("value", out var value)) continue;
            if (node.Properties.TryGetValue("transient", out var transient) && transient.GetBoolean()) continue;
            Assert.That(schema.ContainsKey(node.Id), Is.True, "Every input must save to a declared data key: " + node.Id);
            data[node.Id] = JsonNode.Parse(value.GetRawText());
        }
        var settings = Read(data);
        var hub = new DataHub();
        await using var button = new ButtonSession(ButtonTests.Surface(), settings, hub);
        Assert.That(ButtonTests.Text(button.BuildTree(), ".title"), Is.EqualTo("日本語 <tag> & {{literal}}"));
        Assert.That(ButtonTests.Text(button.BuildTree(), ".value"), Is.EqualTo("25 pts"));
        Assert.That(Node(button.BuildTree(), ".value").Properties["size"].GetProperty("basis").GetDouble(), Is.EqualTo(.24));
        Assert.That(Node(button.BuildTree(), ".content").Properties["background"].GetString(), Is.EqualTo("#123456"));
        Assert.That(Node(button.BuildTree(), ".level").Properties["end"].GetDouble(), Is.EqualTo(.5));
        hub.Update("demo", DataHub.ParseValues("{\"load\":40}")); button.Refresh();
        Assert.That(ButtonTests.Text(button.BuildTree(), ".value"), Is.EqualTo("40 pts"));
        Assert.That(Node(button.BuildTree(), ".level").Properties["end"].GetDouble(), Is.EqualTo(.8));
    }

    [TestCase("horizontal")][TestCase("vertical")]
    public async Task DualMetricsUseIndependentKeysAndRequestedDirection(string direction)
    {
        var data = Data("dual"); data["designDirection"] = direction;
        var hub = new DataHub(); hub.Update("demo", DataHub.ParseValues("{\"value\":12,\"ram\":76}"));
        await using var button = new ButtonSession(ButtonTests.Surface(), Read(data), hub);
        Assert.That(ButtonTests.Text(button.BuildTree(), ".first.value"), Is.EqualTo("12%"));
        Assert.That(ButtonTests.Text(button.BuildTree(), ".second.value"), Is.EqualTo("76%"));
        Assert.That(Node(button.BuildTree(), ".metrics").Properties["direction"].GetString(), Is.EqualTo(direction));
        Assert.That(ButtonTests.Nodes(button.BuildTree().Root).All(n => !n.Properties.ContainsKey("mainSize")), Is.True);
    }

    [Test] public async Task TextStyleAllowsNonNumericDataAndNoBar()
    {
        var data = Data("metric"); data["designUnit"] = "";
        var hub = new DataHub(); hub.Update("demo", DataHub.ParseValues("{\"value\":\"再生中\"}"));
        await using var button = new ButtonSession(ButtonTests.Surface(), Read(data), hub);
        Assert.That(ButtonTests.Text(button.BuildTree(), ".value"), Is.EqualTo("再生中"));
        Assert.That(ButtonTests.Nodes(button.BuildTree().Root).Any(n => n.Type == "ui.range-bar"), Is.False);
    }

    [Test] public async Task InvalidGaugeDataShowsAnErrorAndRecovers()
    {
        var hub = new DataHub(); hub.Update("demo", DataHub.ParseValues("{\"value\":\"bad\"}"));
        await using var button = new ButtonSession(ButtonTests.Surface(), Read(Data()), hub);
        Assert.That(ButtonTests.Text(button.BuildTree(), ".message"), Does.Contain("numeric data"));
        hub.Update("demo", DataHub.ParseValues("{\"value\":75}")); button.Refresh();
        Assert.That(ButtonTests.Text(button.BuildTree(), ".value"), Is.EqualTo("75%"));
    }

    [TestCase("designBarMax", "0")]
    [TestCase("designTextSize", "\"bad\"")]
    [TestCase("designPreset", "\"unknown\"")]
    public async Task InvalidDesignSettingsProduceReadableErrors(string field, string json)
    {
        var data = Data(); data[field] = JsonNode.Parse(json);
        await using var button = new ButtonSession(ButtonTests.Surface(), Read(data), null);
        Assert.That(ButtonTests.Text(button.BuildTree(), ".errorTitle"), Is.EqualTo("Custom Button"));
        Assert.That(ButtonTests.Text(button.BuildTree(), ".message"), Is.Not.Empty);
    }
}

