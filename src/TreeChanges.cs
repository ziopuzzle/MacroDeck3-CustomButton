using System.Text.Json;
using MacroDeck.Ui.Model.Nodes;
using MacroDeck.Ui.Model.Patches;

namespace Ziopuzzle.CustomButton;

/// <summary>Keep live controls and pointer capture when only their properties change.</summary>
public static class TreeChanges
{
    public static IReadOnlyList<UiPatchOperation> Between(UiNode before, UiNode after)
    {
        var changes = new List<UiPatchOperation>();
        void Visit(UiNode old, UiNode next)
        {
            if (old.Id != next.Id || old.Type != next.Type || old.RequiredComponentVersion != next.RequiredComponentVersion
                || !old.Children.Select(c => c.Id).SequenceEqual(next.Children.Select(c => c.Id))
                || !ReferenceEquals(old.Fallback, next.Fallback) && JsonSerializer.Serialize(old.Fallback) != JsonSerializer.Serialize(next.Fallback))
            {
                changes.Add(new() { Op = UiPatchOperations.ReplaceNode, NodeId = old.Id, Node = next });
                return;
            }
            var properties = next.Properties.Where(p => !old.Properties.TryGetValue(p.Key, out var value) || !JsonElement.DeepEquals(value, p.Value)).ToDictionary(p => p.Key, p => p.Value);
            var removed = old.Properties.Keys.Except(next.Properties.Keys).ToArray();
            if (properties.Count > 0 || removed.Length > 0)
                changes.Add(new() { Op = UiPatchOperations.SetProperties, NodeId = next.Id, Properties = properties, RemovedProperties = removed });
            for (int i = 0; i < old.Children.Count; i++) Visit(old.Children[i], next.Children[i]);
        }
        Visit(before, after);
        return changes;
    }
}
