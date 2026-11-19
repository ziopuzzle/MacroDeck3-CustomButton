using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class HistorySamplingTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    [Test] public void SamplingAfterAWriteKeepsTheSnapshotUntilAnotherWriteInTheSameSecond()
    {
        var clock = new Clock(); var hub = new DataHub(clock);
        hub.Update("demo", DataHub.ParseValues("""{"value":42}"""));
        var before = hub.Snapshot("demo");
        hub.Sample("demo", DataHub.ParseValues("""{"value":5}"""));
        Assert.That(hub.Snapshot("demo"), Is.SameAs(before));

        hub.Update("demo", DataHub.ParseValues("""{"value":60}"""));
        var after = hub.Snapshot("demo");
        Assert.That(after, Is.Not.SameAs(before));
        Assert.That(before.ReadHistory("value", 60), Is.EqualTo(new[] { 42d }));
        Assert.That(after.ReadHistory("value", 60), Is.EqualTo(new[] { 60d }));
    }

    [Test] public void UnchangedReadingsAndInitialValuesAdvanceOncePerSecondWithoutNewPackets()
    {
        var clock = new Clock(); var hub = new DataHub(clock); var initial = DataHub.ParseValues("{\"value\":42}");
        hub.Sample("demo", initial); hub.Sample("demo", initial);
        Assert.That(hub.Snapshot("demo").ReadHistory("value", 60), Is.EqualTo(new[] { 42d }));
        Assert.That(hub.Read("demo"), Is.Empty, "Sampling initial data must not turn it into received data.");
        clock.Now = clock.Now.AddSeconds(1); hub.Sample("demo", initial);
        hub.Update("demo", DataHub.ParseValues("{\"value\":80}"));
        Assert.That(hub.Snapshot("demo").ReadHistory("value", 60), Is.EqualTo(new[] { 42d, 80d }));
        clock.Now = clock.Now.AddSeconds(1); hub.Sample("demo", initial);
        Assert.That(hub.Snapshot("demo").ReadHistory("value", 60), Is.EqualTo(new[] { 42d, 80d, 80d }));
        for (var i = 0; i < 150; i++) { clock.Now = clock.Now.AddSeconds(1); hub.Sample("demo", initial); }
        Assert.That(hub.Snapshot("demo").ReadHistory("value", 120), Has.Count.EqualTo(120));
    }
}

