using System.Text.Json;
using MacroDeck.Ui.Model.Events;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class EventTargetTests
{
    private static JsonElement Flows(string eventId = "element-press", string elementId = "label", string target = "$self", string provider = CustomButtonIntegration.PluginId)
        => JsonSerializer.SerializeToElement(new[] { new { triggerType = "onEvent", children = new[] { new { actionId = "keep", value = "$self" } },
            @event = new { providerId = provider, eventId, parameters = new[] { new { name = "widgetId", value = target }, new { name = "elementId", value = elementId } } } } });

    [TestCase("short-press")][TestCase("element-press")][TestCase("element-change")][TestCase("display-tick")]
    public void ResolvesSelfOnlyInOwnedEventTarget(string eventId)
    {
        var original = Flows(eventId);
        var normalized = ButtonEvents.Normalize(original, "test-widget");
        Assert.That(normalized[0].GetProperty("event").GetProperty("parameters")[0].GetProperty("value").GetString(), Is.EqualTo("test-widget"));
        Assert.That(normalized[0].GetProperty("children").GetRawText(), Is.EqualTo(original[0].GetProperty("children").GetRawText()));
        var other = Flows(provider: "other");
        Assert.That(ButtonEvents.Normalize(other, "test-widget").GetRawText(), Is.EqualTo(other.GetRawText()));
        var explicitTarget = Flows(target: "other-widget");
        Assert.That(ButtonEvents.Normalize(explicitTarget, "test-widget")[0].GetProperty("event").GetProperty("parameters").GetRawText(), Is.EqualTo(explicitTarget[0].GetProperty("event").GetProperty("parameters").GetRawText()));
    }

    [Test] public async Task SavedAndNewSelfFiltersAreResolvedInConfigurationValue()
    {
        await using var config = new ConfigurationSession(ButtonTests.Surface("config"), TestLayouts.Sample with { Flows = Flows() });
        string? Target() => ButtonTests.Nodes(config.BuildTree().Root).Single(n => n.Id == "flows").Properties["value"][0].GetProperty("event").GetProperty("parameters")[0].GetProperty("value").GetString();
        Assert.That(Target(), Is.EqualTo("test-widget"));
        config.Dispatch(new UiEvent { NodeId = "flows", Name = "change", Data = Flows("short-press") });
        Assert.That(Target(), Is.EqualTo("test-widget"));
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


