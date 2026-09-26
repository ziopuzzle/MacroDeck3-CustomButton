using System.Text.Json;
using MacroDeck.Plugin.Testing.Fakes;
using MacroDeck.Ui.Model.Events;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class ButtonEventTests
{
    [TestCase("onShortPress", "short-press")]
    [TestCase("onLongPress", "long-press")]
    [TestCase("onTouchStart", "touch-start")]
    [TestCase("onTouchEnd", "touch-end")]
    public void NativeTriggersConvertToScopedEventsWithoutChangingActionBlocks(string trigger, string expected)
    {
        var source = JsonSerializer.SerializeToElement(new[] { new { triggerId = "keep-id", triggerType = trigger, children = new[] { new { id = "block", type = "if", children = new[] { new { type = "wait", value = "{{vars.delay}}" } } } } } });
        var result = ButtonEvents.Normalize(source, "widget-a")[0];
        Assert.That(result.GetProperty("triggerType").GetString(), Is.EqualTo("onEvent"));
        Assert.That(result.GetProperty("triggerId").GetString(), Is.EqualTo("keep-id"));
        Assert.That(result.GetProperty("children").GetRawText(), Is.EqualTo(source[0].GetProperty("children").GetRawText()));
        var binding = result.GetProperty("event");
        Assert.That(binding.GetProperty("eventId").GetString(), Is.EqualTo(expected));
        Assert.That(binding.GetProperty("providerId").GetString(), Is.EqualTo(CustomButtonIntegration.PluginId));
        Assert.That(binding.GetProperty("parameters")[0].GetProperty("value").GetString(), Is.EqualTo("widget-a"));
        Assert.That(binding.GetProperty("parameters")[0].GetProperty("operator").GetString(), Is.EqualTo("=="));
    }

    [Test] public async Task NativeEditorPersistsFlowsAndKeepsThemAcrossLayoutEdits()
    {
        await using var config = new ConfigurationSession(ButtonTests.Surface("config"), TestLayouts.Sample);
        var node = ButtonTests.Nodes(config.BuildTree().Root).Single(n => n.Id == "flows");
        Assert.That(node.Type, Is.EqualTo("actions-list-editor"));
        var initial = node.Properties["value"];
        Assert.That(initial.GetArrayLength(), Is.Zero);
        var changed = JsonSerializer.SerializeToElement(new[] { new { triggerId = "flow", triggerType = "onShortPress", children = new object[0] } });
        config.Dispatch(new UiEvent { NodeId = "flows", Name = "change", Data = changed });
        var stored = ButtonTests.Nodes(config.BuildTree().Root).Single(n => n.Id == "flows").Properties["value"];
        Assert.That(stored[0].GetProperty("triggerType").GetString(), Is.EqualTo("onEvent"));
        config.Dispatch(new UiEvent { NodeId = "layout", Name = "change", Data = JsonSerializer.SerializeToElement("<text id='t'>New</text>") });
        Assert.That(ButtonTests.Nodes(config.BuildTree().Root).Single(n => n.Id == "flows").Properties["value"].GetRawText(), Is.EqualTo(stored.GetRawText()));
        var settings = ButtonSettings.Read(JsonSerializer.SerializeToElement(new { flows = stored }));
        Assert.That(settings.Flows.GetRawText(), Is.EqualTo(stored.GetRawText()));
    }

    [TestCase("widget", false, 4)]
    [TestCase("preview", false, 0)]
    [TestCase("widget", true, 0)]
    public async Task OnlyLiveButtonsPublishGestureEvents(string kind, bool sample, int expected)
    {
        var integration = new CustomButtonIntegration(); var context = new FakeIntegrationContext();
        await integration.InitializeAsync(context);
        await using var session = (await integration.CreateSessionAsync(new() { Surface = ButtonTests.Surface(kind, sample: sample), UiModelVersion = 1 }, default))!;
        foreach (var name in new[] { "press-start", "long-press", "press-end", "press" })
            session.Dispatch(new UiEvent { NodeId = "root", Name = name });
        var events = context.Events.Published.Where(e => e.EventId != ButtonEvents.Tick).ToArray();
        Assert.That(events.Length, Is.EqualTo(expected));
        if (expected > 0)
        {
            Assert.That(events.Select(e => e.EventId), Is.EqualTo(new[] { "touch-start", "long-press", "touch-end", "short-press" }));
            Assert.That(events.All(e => e.Parameters!.Value.GetProperty("widgetId").GetString() == "test-widget"), Is.True);
        }
    }

    [Test] public async Task PreviewEmitsUpdateEventWithoutScriptAndSharesTickerWithLiveWidget()
    {
        var integration = new CustomButtonIntegration(); var context = new FakeIntegrationContext();
        await integration.InitializeAsync(context);
        await using var preview = (await integration.CreateSessionAsync(new() { Surface = ButtonTests.Surface("preview"), UiModelVersion = 1 }, default))!;
        await using var live = (await integration.CreateSessionAsync(new() { Surface = ButtonTests.Surface(), UiModelVersion = 1 }, default))!;
        for (int i = 0; i < 50 && context.Events.Published.Count == 0; i++) await Task.Delay(10);
        Assert.That(context.Events.Published.Count(e => e.EventId == ButtonEvents.Tick), Is.EqualTo(1));
        await live.DisposeAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        while (context.Events.Published.Count(e => e.EventId == ButtonEvents.Tick) < 2) await Task.Delay(20, timeout.Token);
        Assert.That(context.Scripts.Ran, Is.Empty);
    }
}

