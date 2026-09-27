using System.Text.Json;
using MacroDeck.Plugin.Hosting;
using MacroDeck.Plugin.Protocol.Capabilities.Ui;
using MacroDeck.Plugin.Protocol.Serialization;
using MacroDeck.Plugin.Testing;
using MacroDeck.Ui.Model.Nodes;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class ProtocolTests
{
    [Test] public async Task RealProcessRegistersWidgetAndSerializesUpdatedSnapshot()
    {
        // Launch the managed assembly on every OS; the apphost name differs on Windows/Unix.
        var assembly = Path.Combine(AppContext.BaseDirectory, "Ziopuzzle.CustomButton.dll");
        await using var host = await MacroDeckTestHost.StartAsync();
        await using var plugin = await host.LaunchAsync(PluginLaunchSpec.ForDotnet(assembly));
        var connection = await host.WaitForSessionAsync(TimeSpan.FromSeconds(30));
        Assert.That(connection.Declared.Select(c => c.Kind), Does.Contain("widget-type-provider"));
        Assert.That((await connection.WidgetTypeProvider.GetWidgetTypesAsync()).Succeeded, Is.True);
        var action = await connection.Actions.ExecuteAsync("set-value", new Dictionary<string, object?> { ["channel"] = "demo", ["key"] = "value", ["value"] = 81 });
        Assert.That(action.Succeeded, Is.True);
        var opened = await connection.InvokeAsync("ui", "provider", "session.open", new UiSessionOpenArguments
        {
            SessionId = "wire-widget", SurfaceKind = "widget", SessionMode = "shared", UiModelVersion = 1,
            SurfaceAttributes = JsonSerializer.SerializeToElement(ButtonTests.Surface().Attributes)
        });
        Assert.That(opened.Succeeded, Is.True);
        Assert.That(opened.DataAs<UiSessionOpenResult>()!.Accepted, Is.True);
        await connection.InvokeAsync("ui", "provider", "session.snapshot", new UiSessionSnapshotArguments { SessionId = "wire-widget" });
        JsonElement? snapshot = null;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!snapshot.HasValue)
        {
            foreach (var message in host.Messages.OfType("host.invoke"))
            {
                var payload = message.Envelope.Payload!.Value;
                if (payload.GetProperty("api").GetString() == "ui" && payload.GetProperty("operation").GetString() == "snapshot")
                    snapshot = payload.GetProperty("arguments").GetProperty("tree");
            }
            if (!snapshot.HasValue) await Task.Delay(20, timeout.Token);
        }
        var tree = snapshot.Value.Deserialize<UiTree>(PluginProtocolJson.Options)!;
        Assert.That(ButtonTests.Text(tree, ".value"), Is.EqualTo("81%"));
        Assert.That(ButtonTests.Nodes(tree.Root).All(n => n.Type.StartsWith("ui.")), Is.True);
        // The SDK stub records UI callbacks but does not implement the client's renderer/ack path.
        // Tree rendering and patch sequencing are covered separately; this asserts real process transport.
        await connection.InvokeAsync("ui", "provider", "session.close", new UiSessionCloseArguments { SessionId = "wire-widget" });
        var stopped = await plugin.StopGracefullyAsync(TimeSpan.FromSeconds(10));
        Assert.That(stopped.ExitedWithinGrace, Is.True); Assert.That(stopped.Killed, Is.False);
    }
}

