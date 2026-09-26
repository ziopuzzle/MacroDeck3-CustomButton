using System.Text.Json;
using MacroDeck.Ui.Model.Events;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class SettingsRevisionTests
{
    [Test] public async Task DeletedUpdateEventCanBeRestoredWithoutDuplicatingOrReplacingOtherActions()
    {
        var original = ButtonEvents.Normalize(JsonSerializer.SerializeToElement(new[] {
            new { triggerId = "press", triggerType = "onShortPress", children = new[] { new { type = "wait", duration = 42 } } }
        }), "test-widget");
        await using var config = new ConfigurationSession(ButtonTests.Surface("config"), TestLayouts.Sample with { Flows = original });
        JsonElement Flows() => ButtonTests.Nodes(config.BuildTree().Root).Single(n => n.Id == "flows").Properties["value"];
        config.Dispatch(new UiEvent { NodeId = "inputTarget", Name = "change", Data = JsonSerializer.SerializeToElement("$other") });
        void Add() => config.Dispatch(new UiEvent { NodeId = "addInputEvent", Name = "change", Data = JsonSerializer.SerializeToElement(ButtonEvents.Tick) });
        Add();
        var added = Flows();
        Assert.That(added.GetArrayLength(), Is.EqualTo(2));
        Assert.That(added[0].GetRawText(), Is.EqualTo(original[0].GetRawText()));
        Assert.That(added[1].GetProperty("event").GetProperty("eventId").GetString(), Is.EqualTo(ButtonEvents.Tick));
        Add(); Assert.That(Flows().GetRawText(), Is.EqualTo(added.GetRawText()));
        config.Dispatch(new UiEvent { NodeId = "flows", Name = "change", Data = original });
        Add(); Assert.That(Flows().GetArrayLength(), Is.EqualTo(2));
        config.Dispatch(new UiEvent { NodeId = "flows", Name = "change", Data = ButtonEvents.Empty });
        await using var reopened = new ConfigurationSession(ButtonTests.Surface("config"), ButtonSettings.Read(JsonSerializer.SerializeToElement(new { flows = Flows() })));
        Assert.That(ButtonTests.Nodes(reopened.BuildTree().Root).Single(n => n.Id == "flows").Properties["value"].GetArrayLength(), Is.Zero);
    }

    [Test] public void SuggestionsRenderForEverySupportedAttributeAndConditionalStyle()
    {
        foreach (var type in new[] { "stack", "layer", "text", "bar", "chart", "clock", "progress-bar", "slider" })
        foreach (var attribute in ("fill mainSize visible easing transitionMs transitionProperties " + (type switch {
            "stack" => "direction align justify borderStyle", "text" => "align weight role wrap",
            "clock" => "seconds", "slider" => "direction", _ => ""
        })).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        foreach (var value in LayoutOptions.Choices(type, attribute))
        {
            var extra = type == "chart" ? " key='value'" : "";
            foreach (var xml in new[] {
                $"<{type} id='x'{extra} {attribute}='{value}' />",
                $"<{type} id='x'{extra}><style when='value > 0' {attribute}='{value}' /></{type}>" })
                Assert.DoesNotThrow(() => new LayoutRenderer(xml).Render(DataHub.ParseValues("{\"value\":42}")), xml);
        }
        Assert.That(LayoutOptions.Choices("stack", "align"), Does.Contain("stretch").And.Contain("baseline"));
        Assert.That(LayoutOptions.Choices("text", "align"), Does.Not.Contain("stretch"));
    }
}

