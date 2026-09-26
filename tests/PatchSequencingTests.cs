using MacroDeck.Ui.Model.Patches;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class PatchSequencingTests
{
    [Test] public async Task SnapshotSupersedesQueuedPatchAndNextUpdateUsesItsRevision()
    {
        var hub = new DataHub();
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample, hub);
        var client = session.BuildTree();
        for (var value = 1; value <= 40; value++)
        {
            hub.Update("demo", DataHub.ParseValues($"{{\"value\":{value}}}"));
            session.Refresh();
            if (value % 3 == 0)
            {
                client = session.BuildTree();
                Assert.That(session.DrainPatches(), Is.Empty, "A snapshot must supersede pending patches.");
            }
            else
            {
                var patch = session.DrainPatches().Single();
                var outcome = UiPatchSequencing.CheckRevisions(client.Revision, patch);
                Assert.That(outcome.IsApplicable, Is.True, outcome.RejectionReason);
                client = client with { Revision = patch.ToRevision };
            }
        }
    }

    [Test] public async Task BurstReturningToOriginalAppearanceStillPublishesValidRevision()
    {
        var hub = new DataHub();
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample, hub);
        var client = session.BuildTree();
        foreach (var value in new[] { 90, 42 })
        {
            hub.Update("demo", DataHub.ParseValues($"{{\"value\":{value}}}"));
            session.Refresh();
        }
        var patch = session.DrainPatches().Single();
        var outcome = UiPatchSequencing.CheckRevisions(client.Revision, patch);
        Assert.That(outcome.IsApplicable, Is.True, outcome.RejectionReason);
        hub.Update("demo", DataHub.ParseValues("{\"value\":70}"));
        session.Refresh();
        outcome = UiPatchSequencing.CheckRevisions(patch.ToRevision, session.DrainPatches().Single());
        Assert.That(outcome.IsApplicable, Is.True, outcome.RejectionReason);
    }
}

