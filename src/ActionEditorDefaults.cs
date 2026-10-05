using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ziopuzzle.CustomButton;

public static class ActionEditorDefaults
{
    public static JsonElement ChangeChannel(JsonElement value, string previous, string next)
    {
        if (previous == next) return value;
        var flows = JsonNode.Parse(value.GetRawText())!.AsArray();
        foreach (var block in Blocks(flows))
        {
            if (block["integrationId"]?.ToString() != CustomButtonIntegration.PluginId
                || block["actionId"]?.ToString() is not ("set-value" or "set-data")
                || block["parameters"] is not JsonArray parameters) continue;
            foreach (var parameter in parameters.OfType<JsonObject>())
                if (parameter["name"]?.ToString() == "channel" && parameter["value"] is JsonValue raw
                    && raw.TryGetValue<string>(out var text) && text == previous)
                    parameter["value"] = next;
        }
        return JsonSerializer.SerializeToElement(flows);
    }

    public static JsonElement ApplyChannel(JsonElement previous, JsonElement next, string channel)
    {
        var existing = Blocks(JsonNode.Parse(previous.GetRawText())!.AsArray())
            .Select(b => b["id"]?.ToString()).Where(id => !string.IsNullOrEmpty(id)).ToHashSet(StringComparer.Ordinal);
        var flows = JsonNode.Parse(next.GetRawText())!.AsArray();
        foreach (var block in Blocks(flows))
        {
            if (block["id"]?.ToString() is not { Length: > 0 } id || existing.Contains(id)
                || block["integrationId"]?.ToString() != CustomButtonIntegration.PluginId
                || block["actionId"]?.ToString() is not ("set-value" or "set-data")) continue;
            if (block["parameters"] is not JsonArray parameters) continue;
            foreach (var parameter in parameters.OfType<JsonObject>().Where(p => p["name"]?.ToString() == "channel"))
            {
                // Preserve explicitly configured channels on pasted or duplicated actions.
                if (parameter["value"]?.ToString() is not (null or "" or "demo")) continue;
                parameter["value"] = channel;
            }
        }
        return JsonSerializer.SerializeToElement(flows);
    }

    private static IEnumerable<JsonObject> Blocks(JsonArray items)
    {
        foreach (var item in items.OfType<JsonObject>())
        {
            yield return item;
            if (item["children"] is JsonArray children)
                foreach (var child in Blocks(children)) yield return child;
            if (item["branches"] is JsonArray branches)
                foreach (var child in Blocks(branches)) yield return child;
        }
    }

    public static JsonElement RetargetEvents(JsonElement value, string? widgetId, string? sourceWidgetId = null)
    {
        if (string.IsNullOrWhiteSpace(widgetId)) throw new FormatException("Save the button before retargeting events.");
        var flows = JsonNode.Parse(ButtonEvents.Normalize(value, widgetId).GetRawText())!.AsArray();
        foreach (var flow in flows.OfType<JsonObject>())
        {
            if (flow["event"] is not JsonObject binding || binding["parameters"] is not JsonArray parameters) continue;
            foreach (var parameter in parameters.OfType<JsonObject>())
            {
                if (parameter["type"]?.ToString() != "widget-target"
                    && !(binding["providerId"]?.ToString() == CustomButtonIntegration.PluginId && parameter["name"]?.ToString() == "widgetId")) continue;
                if (sourceWidgetId != null && parameter["value"]?.ToString() != sourceWidgetId) continue;
                parameter["value"] = widgetId;
                parameter.Remove("valueLabel");
            }
        }
        return JsonSerializer.SerializeToElement(flows);
    }
}
