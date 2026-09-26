using System.Text.Json;
using MacroDeck.Ui.Model.Events;
using MacroDeck.Ui.Model.Nodes;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class HistoryTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => Now;
        public void Advance() => Now = Now.AddSeconds(1);
    }
    private static double[] Points(UiTree tree) => ButtonTests.Nodes(tree.Root).Single(n => n.Type == "ui.chart")
        .Properties["points"].EnumerateArray().Select(v => v.GetDouble()).ToArray();

    [Test] public void HistoryRetainsRepeatedValuesCoalescesOneSecondAndIsBounded()
    {
        var clock = new Clock(); var hub = new DataHub(clock);
        hub.Update("demo", DataHub.ParseValues("{\"value\":10}"));
        hub.Update("demo", DataHub.ParseValues("{\"value\":20}"));
        Assert.That(hub.ReadHistory("demo", "value", 60), Is.EqualTo(new[] { 20d }));
        clock.Advance(); hub.Update("demo", DataHub.ParseValues("{\"value\":20}"));
        Assert.That(hub.ReadHistory("demo", "value", 60), Is.EqualTo(new[] { 20d, 20d }));
        for (var i = 0; i < 150; i++) { clock.Advance(); hub.Update("demo", DataHub.ParseValues("{\"value\":" + i + "}")); }
        Assert.That(hub.ReadHistory("demo", "value", 120), Has.Count.EqualTo(120));
        Assert.That(hub.ReadHistory("demo", "value", 2), Is.EqualTo(new[] { 148d, 149d }));
        Assert.That(hub.ReadHistory("other", "value", 60), Is.Empty);
    }

    [Test] public void BadValuesAndRemovedKeysClearOnlyTheirHistoryAndFailedUpdatesAreAtomic()
    {
        var hub = new DataHub(); hub.Update("demo", DataHub.ParseValues("{\"value\":10,\"ram\":20}"));
        Assert.Throws<FormatException>(() => hub.Update("demo", new Dictionary<string, JsonElement> { ["value"] = JsonSerializer.SerializeToElement(new[] { 1, 2 }) }));
        Assert.That(hub.ReadHistory("demo", "value", 60), Is.EqualTo(new[] { 10d }));
        hub.Update("demo", DataHub.ParseValues("{\"value\":\"NaN\"}"));
        Assert.That(hub.ReadHistory("demo", "value", 60), Is.Empty);
        Assert.That(hub.ReadHistory("demo", "ram", 60), Is.EqualTo(new[] { 20d }));
        hub.Update("demo", DataHub.ParseValues("{}"), replace: true);
        Assert.That(hub.ReadHistory("demo", "ram", 60), Is.Empty);
    }

    [TestCase("widget_history-chart")]
    public async Task TemplatesRenderTheLiveReadingAndNormalisedHistoryInSeparateLayers(string template)
    {
        var clock = new Clock(); var hub = new DataHub(clock);
        var settings = TestLayouts.Sample with { Layout = LayoutTemplates.Get(template).Xml };
        await using var session = new ButtonSession(ButtonTests.Surface(), settings, hub);
        Assert.That(Points(session.BuildTree()), Is.Empty, "Initial display data must not invent history.");
        foreach (var value in new[] { -10, 25, 150 })
        { clock.Advance(); hub.Update("demo", DataHub.ParseValues("{\"chart_value\":" + value + "}")); }
        session.Refresh();
        Assert.That(Points(session.BuildTree()), Is.EqualTo(new[] { 0d, .25, 1d }));
        Assert.That(ButtonTests.Text(session.BuildTree(), ".value"), Is.EqualTo("150"));
        var chart = ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Type == "ui.chart");
        Assert.That(chart.Properties["plotTop"].GetDouble(), Is.EqualTo(.66));
        Assert.That(ButtonTests.Nodes(session.BuildTree().Root).All(n => !n.Properties.ContainsKey("mainSize")), Is.True);
        session.Refresh(); session.Refresh();
        Assert.That(hub.ReadHistory("demo", "chart_value", 40), Has.Count.EqualTo(3), "Rendering must not append history.");
        await using var reopened = new ButtonSession(ButtonTests.Surface(), settings, hub);
        Assert.That(Points(reopened.BuildTree()), Is.EqualTo(Points(session.BuildTree())));
    }

    [Test] public async Task TemplateDropdownAppliesImmediatelyAndPreservesOtherSettings()
    {
        var settings = TestLayouts.Sample with { Channel = "my-channel" };
        await using var config = new ConfigurationSession(ButtonTests.Surface("config"), settings);
        UiNode Node(string id) => ButtonTests.Nodes(config.BuildTree().Root).Single(n => n.Id == id || n.Id.EndsWith("." + id));
        var picker = Node("templatePicker");
        Assert.That(picker.Properties["transient"].GetBoolean(), Is.True);
        var originalCount = ButtonTests.Nodes(config.BuildTree().Root).Count();
        config.Dispatch(new UiEvent { NodeId = picker.Id, Name = "change", Data = JsonSerializer.SerializeToElement("widget_history-chart") });
        Assert.That(Node("layout").Properties["value"].GetString(), Is.EqualTo(LayoutTemplates.Get("widget_history-chart").Xml));
        config.Dispatch(new UiEvent { NodeId = picker.Id, Name = "change", Data = JsonSerializer.SerializeToElement("basic_bar") });
        Assert.That(Node("initialValues").Properties["value"].GetString(), Is.EqualTo(LayoutTemplates.Get("basic_bar").InitialValues));
        config.Dispatch(new UiEvent { NodeId = picker.Id, Name = "change", Data = JsonSerializer.SerializeToElement("widget_history-chart") });
        Assert.That(Node("channel").Properties["value"].GetString(), Is.EqualTo(settings.Channel));
        Assert.That(Node("initialValues").Properties["value"].GetString(), Is.EqualTo(LayoutTemplates.Get("widget_history-chart").InitialValues));
        Assert.That(ButtonTests.Nodes(config.BuildTree().Root).Count(), Is.EqualTo(originalCount));
        config.Dispatch(new UiEvent { NodeId = "layout", Name = "change", Data = JsonSerializer.SerializeToElement("<text id='x'>Edited</text>") });
        Assert.That(Node("templatePicker").Properties["value"].GetString(), Is.Empty);
        config.Dispatch(new UiEvent { NodeId = picker.Id, Name = "change", Data = JsonSerializer.SerializeToElement("widget_history-chart") });
        Assert.That(Node("layout").Properties["value"].GetString(), Is.EqualTo(LayoutTemplates.Get("widget_history-chart").Xml));
    }
    [Test] public async Task ChartSupportsAnArbitraryDataKeyAndScale()
    {
        var hub = new DataHub(); hub.Update("demo", DataHub.ParseValues("{\"temperature\":50,\"value\":90}"));
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with
        { Layout = "<chart id=\"chart\" key=\"temperature\" min=\"20\" max=\"80\" points=\"30\"/>" }, hub);
        Assert.That(Points(session.BuildTree()), Is.EqualTo(new[] { .5 }));
    }
}


