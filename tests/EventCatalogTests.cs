using System.Text.Json;
using MacroDeck.Plugin.Hosting;
using MacroDeck.Plugin.Hosting.Capabilities;
using MacroDeck.Sdk;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class EventCatalogTests
{
    [Test] public async Task SdkDescribeMatchesShortcutsAndIncludesCoordinatePayloads()
    {
        // Exercise the installed SDK's actual wire mapper, not just our in-memory definitions.
        var type = typeof(MacroDeckPlugin).Assembly.GetType("MacroDeck.Plugin.Hosting.Capabilities.Events.EventsCapabilityHandler", throwOnError: true)!;
        var handler = (ICapabilityHandler)Activator.CreateInstance(type,
            new IPluginIntegration[] { new CustomButtonIntegration() },
            new PluginMetadata { Id = CustomButtonIntegration.PluginId, Name = "Custom Button", Version = "0.39.3" })!;
        var response = await handler.InvokeAsync(new CapabilityInvocation
        {
            Kind = "events", LocalId = "provider", Operation = "describe", CorrelationId = "test", Services = null!
        }, default);
        Assert.That(response.IsFailure, Is.False);
        var events = response.Data!.Value.GetProperty("events").EnumerateArray().ToArray();
        const string layout = "<trackpad id='pad' interactive='true'/>";
        foreach (var suffix in new[] { "start", "changing", "end" })
        {
            var id = "trackpad-" + suffix;
            var definition = events.Single(e => e.GetProperty("localId").GetString() == id);
            Assert.That(definition.GetProperty("name").GetRawText(), Does.Contain("Trackpad " + suffix));
            Assert.That(definition.GetProperty("payloadParameters").EnumerateArray().Select(p => p.GetProperty("name").GetString()),
                Is.SupersetOf(new[] { "x", "y", "startX", "startY", "previousX", "previousY", "levelX", "levelY", "keyX", "keyY" }));
            var flow = ButtonEvents.AddElementEvent(ButtonEvents.Empty, "widget", id, "pad", layout)[0].GetProperty("event");
            Assert.That(flow.GetProperty("eventId").GetString(), Is.EqualTo(id));
            Assert.That(flow.GetProperty("parameters").EnumerateArray().Single(p => p.GetProperty("name").GetString() == "elementId").GetProperty("value").GetString(), Is.EqualTo("pad"));
            Assert.That(ButtonEvents.ElementTargetHelp(JsonSerializer.SerializeToElement(new[] { new { @event = flow } }), layout, "widget"), Does.Not.Contain("not a target"));
        }
        Assert.That(events.Select(e => e.GetProperty("localId").GetString()), Does.Contain("element-adjust").And.Contain("element-change"));
    }
}
