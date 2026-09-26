using System.Text.Json;
using MacroDeck.Ui.Model.Nodes;
using MacroDeck.Ui.Model.Patches;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class TreeChangesTests
{
    // A minimal client applies the actual patch, rather than merely checking operation names.
    private static UiNode Apply(UiNode root, IReadOnlyList<UiPatchOperation> changes)
    {
        foreach (var change in changes)
        {
            UiNode Visit(UiNode node)
            {
                if (node.Id != change.NodeId) return node with { Children = node.Children.Select(Visit).ToArray() };
                if (change.Op == UiPatchOperations.ReplaceNode) return change.Node!;
                Assert.That(change.Op, Is.EqualTo(UiPatchOperations.SetProperties));
                var properties = node.Properties.ToDictionary(p => p.Key, p => p.Value);
                foreach (var name in change.RemovedProperties ?? []) properties.Remove(name);
                foreach (var property in change.Properties ?? new Dictionary<string, JsonElement>()) properties[property.Key] = property.Value;
                return node with { Properties = properties };
            }
            root = Visit(root);
        }
        return root;
    }
    [Test] public async Task CoalescedPatchesReproduceSnapshotAcrossVisibilityAndPropertyRemoval()
    {
        var hub = new DataHub();
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with { Layout = "<stack id='panel'><stack id='conditional' background='#123456'><style when='value > 50' borderStyle='static'/><text id='label' visibleWhen='value > 20'>{{value}}</text></stack><slider id='slider' value='{{value}}'/></stack>" }, hub);
        var client = session.BuildTree().Root;
        foreach (var batch in new[] { new[] { 80, 10, 60 }, new[] { 10, 90, 30 }, new[] { 80, 30 } })
        {
            foreach (var value in batch) { hub.Update("demo", DataHub.ParseValues("{\"value\":" + value + "}")); session.Refresh(); }
            var patch = session.DrainPatches().Single();
            Assert.That(patch.Operations.Any(p => p.Op == UiPatchOperations.ReplaceNode && p.NodeId == "root"), Is.False);
            client = Apply(client, patch.Operations);
            Assert.That(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(client), JsonSerializer.SerializeToElement(session.BuildTree().Root)), Is.True);
        }
    }
    [Test] public async Task RemovingRoleDerivedColorUsesRemovedProperties()
    {
        var hub = new DataHub();
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with { Layout = "<text id='label' role='primary'><style when='value > 50' color='#ff0000'/>Text</text>" }, hub);
        hub.Update("demo", DataHub.ParseValues("{\"value\":80}")); session.Refresh(); session.DrainPatches();
        var client = session.BuildTree().Root;
        hub.Update("demo", DataHub.ParseValues("{\"value\":10}")); session.Refresh();
        var patch = session.DrainPatches().Single();
        Assert.That(patch.Operations.Single().RemovedProperties, Does.Contain("color"));
        Assert.That(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(Apply(client, patch.Operations)), JsonSerializer.SerializeToElement(session.BuildTree().Root)), Is.True);
    }
}

