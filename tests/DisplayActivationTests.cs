using System.Text.Json;
using MacroDeck.Plugin.Testing.Fakes;
using MacroDeck.Sdk.Events;
using MacroDeck.Ui.Model.Events;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class DisplayActivationTests
{
    private sealed class Clock : TimeProvider
    {
        public long Time;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => Time;
    }
    private static EventBinding Binding(string widgetId) => new()
    {
        EventId = ButtonEvents.Activated,
        Parameters = new Dictionary<string, EventBindingValue> { ["widgetId"] = new(JsonSerializer.SerializeToElement(widgetId), "==") }
    };
    [Test] public void WaitsForBothSnapshotAndTargetBindingWithoutRepeating()
    {
        var context = new FakeIntegrationContext();
        using var service = new DisplayActivation(context.Events);
        using var lease = service.Acquire("one");
        lease.Ready(); Assert.That(context.Events.Published, Is.Empty);
        context.Events.SetBindings([Binding("another")]); Assert.That(context.Events.Published, Is.Empty);
        context.Events.SetBindings([Binding("one")]);
        lease.Ready(); context.Events.SetBindings([Binding("one"), Binding("another")]);
        Assert.That(context.Events.Published.Count, Is.EqualTo(1));
        Assert.That(context.Events.Published[0].Parameters!.Value.GetProperty("widgetId").GetString(), Is.EqualTo("one"));
    }
    [Test] public void MultipleClientsAndImmediateReplacementShareActivationButLaterReturnRunsAgain()
    {
        var context = new FakeIntegrationContext(); context.Events.SetBindings([Binding("one")]);
        var clock = new Clock(); using var service = new DisplayActivation(context.Events, clock);
        var first = service.Acquire("one"); var second = service.Acquire("one");
        Assert.That(context.Events.Published, Is.Empty);
        first.Ready(); second.Ready(); first.Dispose(); second.Dispose();
        clock.Time = 100; using (var replacement = service.Acquire("one")) replacement.Ready();
        Assert.That(context.Events.Published.Count, Is.EqualTo(1));
        clock.Time = 2100; using var returned = service.Acquire("one"); returned.Ready();
        Assert.That(context.Events.Published.Count, Is.EqualTo(2));
    }
    [Test] public void ClosingBeforeBindingAndShutdownCannotPublishLater()
    {
        var context = new FakeIntegrationContext(); using var service = new DisplayActivation(context.Events);
        var first = service.Acquire("one"); first.Ready(); first.Dispose();
        context.Events.SetBindings([Binding("one")]); Assert.That(context.Events.Published, Is.Empty);
        using var second = service.Acquire("two"); second.Ready(); service.Dispose();
        context.Events.SetBindings([Binding("two")]); Assert.That(context.Events.Published, Is.Empty);
    }
    [TestCase("widget", false, false, 1)]
    [TestCase("preview", false, false, 0)]
    [TestCase("widget", true, false, 0)]
    [TestCase("widget", false, true, 0)]
    public async Task OnlyRealWidgetSnapshotsActivate(string kind, bool sample, bool ghost, int count)
    {
        var integration = new CustomButtonIntegration(); var context = new FakeIntegrationContext();
        context.Events.SetBindings([Binding("test-widget")]); await integration.InitializeAsync(context);
        var surface = ButtonTests.Surface(kind, sample: sample);
        var attrs = surface.Attributes.ToDictionary(p => p.Key, p => p.Value); attrs["ghost"] = JsonSerializer.SerializeToElement(ghost);
        await using var session = (await integration.CreateSessionAsync(new() { Surface = surface with { Attributes = attrs }, UiModelVersion = 1 }, default))!;
        Assert.That(context.Events.Published.Count(e => e.EventId == ButtonEvents.Activated), Is.Zero);
        session.BuildTree(); session.BuildTree();
        Assert.That(context.Events.Published.Count(e => e.EventId == ButtonEvents.Activated), Is.EqualTo(count));
        await integration.ShutdownAsync();
    }
    [Test] public async Task ShortcutAddsOneEditableFlowWithConcreteTarget()
    {
        await using var session = new ConfigurationSession(ButtonTests.Surface("config"), TestLayouts.Sample);
        session.Dispatch(new UiEvent { NodeId = "inputTarget", Name = "change", Data = JsonSerializer.SerializeToElement("$other") });
        for (var i = 0; i < 2; i++) session.Dispatch(new UiEvent { NodeId = "addInputEvent", Name = "change", Data = JsonSerializer.SerializeToElement(ButtonEvents.Activated) });
        var flows = ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id == "flows").Properties["value"];
        Assert.That(flows.GetArrayLength(), Is.EqualTo(1));
        var binding = flows[0].GetProperty("event");
        Assert.That(binding.GetProperty("eventId").GetString(), Is.EqualTo(ButtonEvents.Activated));
        Assert.That(binding.GetProperty("parameters")[0].GetProperty("value").GetString(), Is.EqualTo("test-widget"));
    }
}

