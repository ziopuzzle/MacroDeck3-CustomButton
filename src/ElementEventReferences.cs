using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Ziopuzzle.CustomButton;

internal static class ElementEventReferences
{
    // Only infer ID-only edits. Structural changes cannot reliably identify a renamed element.
    internal static Dictionary<string, string> Renames(LayoutDocument before, LayoutDocument after)
    {
        XElement Shape(LayoutDocument document)
        {
            var root = XElement.Parse(document.Serialize(), LoadOptions.PreserveWhitespace);
            foreach (var element in root.DescendantsAndSelf())
            {
                element.Attribute("id")?.Remove();
                var attributes = element.Attributes().OrderBy(a => a.Name.ToString(), StringComparer.Ordinal).ToArray();
                element.ReplaceAttributes(attributes);
            }
            return root;
        }
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!XNode.DeepEquals(Shape(before), Shape(after))) return result;
        var oldNodes = before.Components.ToArray();
        var newNodes = after.Components.ToArray();
        var oldIds = oldNodes.Select(n => (string?)n.Attribute("id")).ToHashSet();
        var newIds = newNodes.Select(n => (string?)n.Attribute("id")).ToHashSet();
        foreach (var (oldNode, newNode) in oldNodes.Zip(newNodes))
        {
            var oldId = (string?)oldNode.Attribute("id");
            var newId = (string?)newNode.Attribute("id");
            if (!string.IsNullOrEmpty(oldId) && !string.IsNullOrEmpty(newId) && oldId != newId
                && !newIds.Contains(oldId) && !oldIds.Contains(newId)) result[oldId] = newId;
        }
        return result;
    }

    internal static JsonElement Retarget(JsonElement value, string? widgetId, IReadOnlyDictionary<string, string> renames)
    {
        if (string.IsNullOrWhiteSpace(widgetId) || renames.Count == 0) return value;
        var flows = JsonNode.Parse(value.GetRawText())!.AsArray();
        foreach (var flow in flows.OfType<JsonObject>())
        {
            if (flow["event"] is not JsonObject binding || binding["providerId"]?.ToString() != CustomButtonIntegration.PluginId
                || binding["parameters"] is not JsonArray parameters) continue;
            var target = parameters.OfType<JsonObject>().FirstOrDefault(p => p["name"]?.ToString() == "widgetId")?["value"]?.ToString();
            if (target != widgetId && target != "$self") continue;
            foreach (var parameter in parameters.OfType<JsonObject>().Where(p => p["name"]?.ToString() == "elementId"))
                if (parameter["value"] is JsonValue raw && raw.TryGetValue<string>(out var id) && renames.TryGetValue(id, out var next))
                { parameter["value"] = next; parameter.Remove("valueLabel"); }
        }
        return ButtonEvents.Normalize(JsonSerializer.SerializeToElement(flows), widgetId);
    }
}
