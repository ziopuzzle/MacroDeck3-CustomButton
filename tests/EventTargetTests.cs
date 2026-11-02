using System.Text.Json;
using MacroDeck.Ui.Model.Events;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class EventTargetTests
{
    private static JsonElement Flows(string eventId = "element-press", string elementId = "label", string target = "$self", string provider = CustomButtonIntegration.PluginId)
        => JsonSerializer.SerializeToElement(new[] { new { triggerType = "onEvent", children = new[] { new { actionId = "keep", value = "$self" } },
            @event = new { providerId = provider, eventId, parameters = new[] { new { name = "widgetId", value = target }, new { name = "elementId", value = elementId } } } } });

    [TestCase("short-press")][TestCase("element-press")][TestCase("element-change")]
    [TestCase("display-activated")][TestCase("modal-opened")][TestCase("modal-closed")]
    public void PublishedEventDefinitionsAllowSelf(string eventId)
    {
        var target = ButtonEvents.Definitions.Single(d => d.Id == eventId).ConfigurationParameters.Single(p => p.Name == "widgetId");
        Assert.That(target.AllowSelf, Is.True);
    }

    [Test]
    public void ShortcutUsesSelfAndRecognizesLegacyConcreteTargetAsDuplicate()
    {
        const string layout = "<stack id='root'><stack id='play' interactive='true'/></stack>";
        var added = ButtonEvents.AddElementEvent(ButtonEvents.Empty, "test-widget", "element-press", "play", layout);
        var parameter = added[0].GetProperty("event").GetProperty("parameters")[0];
        Assert.That(parameter.GetProperty("value").GetString(), Is.EqualTo("$self"));
        Assert.That(parameter.GetProperty("allowSelf").GetBoolean(), Is.True);
        var legacy = Flows(elementId: "play", target: "test-widget");
        Assert.That(ButtonEvents.AddElementEvent(legacy, "test-widget", "element-press", "play", layout).GetArrayLength(), Is.EqualTo(1));
        var other = Flows(elementId: "play", target: "another-widget");
        Assert.That(ButtonEvents.AddElementEvent(other, "test-widget", "element-press", "play", layout).GetArrayLength(), Is.EqualTo(2));
    }

    [Test]
    public void MigrationOnlyConvertsKnownOwnWidgetTargets()
    {
        var flows = JsonSerializer.Deserialize<JsonElement>("""
        [{"event":{"providerId":"net.ziopuzzle.custombutton","eventId":"short-press","parameters":[{"name":"widgetId","value":"test-widget","operator":"==","valueLabel":"Old"}]},"children":[{"value":"test-widget"}]},
         {"event":{"providerId":"net.ziopuzzle.custombutton","eventId":"modal-closed","parameters":[{"name":"widgetId","value":"test-widget","operator":"!="}]}},
         {"event":{"providerId":"net.ziopuzzle.custombutton","eventId":"element-press","parameters":[{"name":"widgetId","value":"other"}]}},
         {"event":{"providerId":"other","eventId":"custom","parameters":[{"name":"widgetId","value":"test-widget"}]}},
         {"event":{"providerId":"net.ziopuzzle.custombutton","eventId":"unknown","parameters":[{"name":"widgetId","value":"test-widget"}]}},
         {"event":{"providerId":"net.ziopuzzle.custombutton","eventId":"short-press","parameters":[{"name":"widgetId","value":"test-widget","operator":"contains"}]}}]
        """);
        var (result, changed) = ButtonEvents.MigrateOwnTargets(flows, "test-widget");
        Assert.That(changed, Is.True);
        Assert.That(result[0].GetProperty("event").GetProperty("parameters")[0].GetProperty("value").GetString(), Is.EqualTo("$self"));
        Assert.That(result[0].GetProperty("event").GetProperty("parameters")[0].TryGetProperty("valueLabel", out _), Is.False);
        Assert.That(result[1].GetProperty("event").GetProperty("parameters")[0].GetProperty("value").GetString(), Is.EqualTo("$self"));
        Assert.That(result[0].GetProperty("children").GetRawText(), Is.EqualTo(flows[0].GetProperty("children").GetRawText()));
        for (var i = 2; i < flows.GetArrayLength(); i++) Assert.That(result[i].GetRawText(), Is.EqualTo(flows[i].GetRawText()));
        Assert.That(ButtonEvents.MigrateOwnTargets(result, "test-widget").Changed, Is.False);
    }

    [Test] public async Task ExistingOwnTargetPublishesCorrectionWithoutOtherEdits()
    {
        await using var config = new ConfigurationSession(ButtonTests.Surface("config"), TestLayouts.Sample with
            { ConfigurationWidgetId = "test-widget", Flows = Flows(target: "test-widget") });
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        config.Changed += (_, _) => changed.TrySetResult();
        var initial = config.BuildTree();
        Assert.That(ButtonTests.Nodes(initial.Root).Any(n => n.Id.EndsWith("copiedWidgetNotice")), Is.True);
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var patch = config.DrainPatches().SelectMany(p => p.Operations).Single(p => p.NodeId == "flows");
        Assert.That(patch.Properties!["value"][0].GetProperty("event").GetProperty("parameters")[0].GetProperty("value").GetString(), Is.EqualTo("$self"));
    }

    [TestCase("short-press")][TestCase("element-press")][TestCase("element-change")][TestCase("display-tick")]
    public void PreservesSelfInOwnedEventTarget(string eventId)
    {
        var original = Flows(eventId);
        var normalized = ButtonEvents.Normalize(original, "test-widget");
        Assert.That(normalized[0].GetProperty("event").GetProperty("parameters")[0].GetProperty("value").GetString(), Is.EqualTo("$self"));
        Assert.That(normalized[0].GetProperty("children").GetRawText(), Is.EqualTo(original[0].GetProperty("children").GetRawText()));
        var other = Flows(provider: "other");
        Assert.That(ButtonEvents.Normalize(other, "test-widget").GetRawText(), Is.EqualTo(other.GetRawText()));
        var explicitTarget = Flows(target: "other-widget");
        Assert.That(ButtonEvents.Normalize(explicitTarget, "test-widget")[0].GetProperty("event").GetProperty("parameters").GetRawText(), Is.EqualTo(explicitTarget[0].GetProperty("event").GetProperty("parameters").GetRawText()));
    }

    [Test] public async Task SavedAndNewSelfFiltersRemainRelativeInConfigurationValue()
    {
        await using var config = new ConfigurationSession(ButtonTests.Surface("config"), TestLayouts.Sample with { Flows = Flows() });
        string? Target() => ButtonTests.Nodes(config.BuildTree().Root).Single(n => n.Id == "flows").Properties["value"][0].GetProperty("event").GetProperty("parameters")[0].GetProperty("value").GetString();
        Assert.That(Target(), Is.EqualTo("$self"));
        config.Dispatch(new UiEvent { NodeId = "flows", Name = "change", Data = Flows("short-press") });
        Assert.That(Target(), Is.EqualTo("$self"));
    }

    [Test] public async Task RepeatedSelfSelectionKeepsTheRelativeTarget()
    {
        await using var config = new ConfigurationSession(ButtonTests.Surface("config"), TestLayouts.Sample with { Flows = Flows(target: "other-widget") });
        var revision = config.BuildTree().Revision;
        for (var i = 0; i < 3; i++)
        {
            config.Dispatch(new UiEvent { NodeId = "flows", Name = "change", Revision = revision, Data = Flows() });
            var patches = config.DrainPatches();
            Assert.That(patches, Is.Not.Empty);
            Assert.That(patches[0].FromRevision, Is.EqualTo(revision));
            var echoed = patches.SelectMany(p => p.Operations).Last(p => p.NodeId == "flows" && p.Properties?.ContainsKey("value") == true).Properties!["value"];
            Assert.That(echoed[0].GetProperty("event").GetProperty("parameters")[0].GetProperty("value").GetString(), Is.EqualTo("$self"));
            revision = patches[^1].ToRevision;
            Assert.That(config.BuildTree().Revision, Is.EqualTo(revision));
        }
        config.Dispatch(new UiEvent { NodeId = "flows", Name = "change", Revision = revision, Data = Flows(target: "other-widget") });
        var next = config.DrainPatches();
        Assert.That(next[0].FromRevision, Is.EqualTo(revision));
        var value = ButtonTests.Nodes(config.BuildTree().Root).Single(n => n.Id == "flows").Properties["value"];
        Assert.That(value[0].GetProperty("event").GetProperty("parameters")[0].GetProperty("value").GetString(), Is.EqualTo("other-widget"));
    }

    [Test] public void HelpDistinguishesDecorativeLabelsAndInteractiveParents()
    {
        const string layout = "<stack id='panel'><stack id='play' interactive='true'><text id='label'>Play</text></stack><slider id='volume' key='volume' interactive='true'/></stack>";
        Assert.That(ButtonEvents.ElementTargetHelp(Flows(), layout, "test-widget"), Does.Contain("Use its parent ID 'play'"));
        Assert.That(ButtonEvents.ElementTargetHelp(Flows(elementId: "play"), layout, "test-widget"), Does.Not.Contain("not interactive"));
        Assert.That(ButtonEvents.ElementTargetHelp(Flows("element-change", "play"), layout, "test-widget"), Does.Contain("not a target"));
        Assert.That(ButtonEvents.ElementTargetHelp(Flows("element-change", "volume"), layout, "test-widget"), Does.Not.Contain("not a target"));
        Assert.That(ButtonEvents.ElementTargetHelp(Flows(target: "another-widget"), layout, "test-widget"), Does.Not.Contain("not interactive"));
    }
}


