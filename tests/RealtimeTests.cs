using System.Diagnostics;
using System.Text.Json;
using MacroDeck.Ui.Model.Events;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class RealtimeTests
{
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeSeconds(1000);
    }

    [Test] public async Task BurstsDeliverLatestValueToAllViewsWithoutQueuingEveryReading()
    {
        var hub = new DataHub(new FixedClock());
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample, hub);
        await using var preview = new ButtonSession(ButtonTests.Surface("preview"), TestLayouts.Sample, hub);
        await using var unrelated = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with { Channel = "other" }, hub);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int notifications = 0;
        var delivered = new List<MacroDeck.Ui.Model.Patches.UiPatch>();
        session.Changed += (_, _) =>
        {
            if (Interlocked.Increment(ref notifications) == 1)
            {
                entered.TrySetResult();
                release.Wait(TimeSpan.FromSeconds(5));
            }
            delivered.AddRange(session.DrainPatches());
            if (ButtonTests.Text(session.BuildTree(), ".value") == "1000%") finished.TrySetResult();
        };
        try
        {
            hub.Update("demo", DataHub.ParseValues("{\"value\":0}"));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            // The render consumer is deliberately blocked; writers must still make progress.
            for (int value = 1; value <= 1000; value++)
                hub.Update("demo", DataHub.ParseValues($"{{\"value\":{value}}}"));
            release.Set();
            await finished.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.That(notifications, Is.EqualTo(2));
            Assert.That(hub.ReadHistory("demo", "value", 120), Is.EqualTo(new[] { 1000d }));
            Assert.That(ButtonTests.Text(unrelated.BuildTree(), ".value"), Is.EqualTo("42%"));
            for (int i = 0; i < 100 && ButtonTests.Text(preview.BuildTree(), ".value") != "1000%"; i++) await Task.Delay(10);
            Assert.That(ButtonTests.Text(preview.BuildTree(), ".value"), Is.EqualTo("1000%"));
            Assert.That(delivered.Count, Is.EqualTo(2));
            Assert.That(delivered[0].FromRevision, Is.EqualTo(0));
            var patch = delivered[1];
            Assert.That(patch.FromRevision, Is.EqualTo(delivered[0].ToRevision));
            Assert.That(patch.ToRevision, Is.EqualTo(session.BuildTree().Revision));
            Assert.That(patch.Operations.All(p => p.Op == "set-properties"), Is.True);
        }
        finally { release.Set(); }
    }

    [Test] public async Task InputsSurviveDataPatchesButNotRemovedAndRecreatedTargets()
    {
        var hub = new DataHub();
        var inputs = new List<ControlInput>();
        var presses = new List<string>();
        var settings = new ButtonSettings("demo", """
            <stack id="panel">
              <stack id="play" interactive="true" visibleWhen="show == 1"><text id="label">{{value}}</text></stack>
              <slider id="slider" interactive="true" key="value"/>
            </stack>
            """, "{\"show\":1,\"value\":0}");
        await using var session = new ButtonSession(ButtonTests.Surface(), settings, hub,
            onWidgetEvent: presses.Add, onControlInput: inputs.Add);
        string Id(string suffix) => ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id.EndsWith("." + suffix)).Id;
        var original = session.BuildTree().Revision;
        var play = Id("play"); var slider = Id("slider");
        hub.Update("demo", DataHub.ParseValues("{\"value\":30}")); session.Refresh();
        session.Dispatch(new UiEvent { NodeId = "root", Name = "press", Revision = original });
        session.Dispatch(new UiEvent { NodeId = play, Name = "press", Revision = original });
        session.Dispatch(new UiEvent { NodeId = slider, Name = "change", Revision = original, Data = JsonSerializer.SerializeToElement(.75) });
        Assert.That(presses, Is.EqualTo(new[] { "short-press" }));
        Assert.That(inputs.Count, Is.EqualTo(2));
        Assert.That(hub.Read("demo")["value"].GetDouble(), Is.EqualTo(75));
        hub.Update("demo", DataHub.ParseValues("{\"show\":0}")); session.Refresh();
        hub.Update("demo", DataHub.ParseValues("{\"show\":1}")); session.Refresh();
        session.Dispatch(new UiEvent { NodeId = play, Name = "press", Revision = original });
        session.Dispatch(new UiEvent { NodeId = play, Name = "press", Revision = session.BuildTree().Revision + 1 });
        Assert.That(inputs.Count, Is.EqualTo(2));
        session.Dispatch(new UiEvent { NodeId = Id("play"), Name = "press", Revision = session.BuildTree().Revision });
        Assert.That(inputs.Count, Is.EqualTo(3));
    }

    [Test] public async Task PushedUpdatesContinueAndStopAfterDisposal()
    {
        var hub = new DataHub();
        var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample, hub);
        var notices = System.Threading.Channels.Channel.CreateUnbounded<bool>();
        session.Changed += (_, _) => notices.Writer.TryWrite(true);
        var measurements = new List<double>();
        try
        {
            for (int value = 0; value < 20; value++)
            {
                var timer = Stopwatch.StartNew();
                hub.Update("demo", DataHub.ParseValues($"{{\"value\":{value}}}"));
                await notices.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
                measurements.Add(timer.Elapsed.TotalMilliseconds);
                Assert.That(ButtonTests.Text(session.BuildTree(), ".value"), Is.EqualTo(value + "%"));
            }
        }
        finally { await session.DisposeAsync(); }
        TestContext.Out.WriteLine($"Headless update-to-notification: median {measurements.Order().ElementAt(10):F1} ms, maximum {measurements.Max():F1} ms (not client latency).");
        var revision = session.BuildTree().Revision;
        hub.Update("demo", DataHub.ParseValues("{\"value\":999}"));
        await Task.Delay(150);
        Assert.That(session.BuildTree().Revision, Is.EqualTo(revision));
        Assert.That(notices.Reader.TryRead(out _), Is.False);
    }
}

