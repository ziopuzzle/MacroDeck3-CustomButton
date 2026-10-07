using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Events;

namespace Ziopuzzle.CustomButton;

public static class ButtonEvents
{
    public const string Tick = "display-tick";
    public const string Activated = "display-activated";
    public static readonly IReadOnlyDictionary<string, string> TriggerEvents = new Dictionary<string, string>
    {
        ["onShortPress"] = "short-press", ["onLongPress"] = "long-press",
        ["onTouchStart"] = "touch-start", ["onTouchEnd"] = "touch-end"
    };
    private static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>
    {
        ["short-press"] = "Short press", ["long-press"] = "Long press", ["touch-start"] = "Touch start", ["touch-end"] = "Touch end", [Tick] = "Display update (1 second)", [Activated] = "Display activated"
    };
    private static readonly IReadOnlyDictionary<string, string> ControlNames = new Dictionary<string, string>
    {
        ["press"] = "Button press", ["long-press"] = "Button long press", ["press-start"] = "Button touch start", ["press-end"] = "Button touch end",
        ["adjust"] = "Value changing", ["change"] = "Value changed",
        ["position-start"] = "Trackpad start", ["position-changing"] = "Trackpad changing", ["position-end"] = "Trackpad end"
    };
    public static IReadOnlyList<EventDefinition> Definitions { get; } = Names.Select(p => new EventDefinition
    {
        Id = p.Key, Name = TextCatalog.Reference(p.Value), Description = TextCatalog.Reference(p.Key == Activated
            ? "Read current values when the saved button becomes visible. Shared across clients; reopening within 2 seconds counts as the same activation. Does not run for editor previews."
            : "Runs while the target button is displayed or interacted with. Check the target after duplicating a button."),
        ConfigurationParameters = [ActionParameter.WidgetTarget("widgetId", new WidgetTargetOptions { Label = TextCatalog.Reference("Target widget"), Required = true, AllowSelf = false })],
        PayloadParameters = [ActionParameter.Text("widgetId", label: TextCatalog.Reference("Target widget ID"), required: true)]
    }).Concat(ControlNames.Select(p => new EventDefinition
    {
        Id = ControlEventId(p.Key), Name = TextCatalog.Reference(p.Value),
        Description = TextCatalog.Reference("Select an interactive component by its XML id. Slider and dial values use min-max; toggles report 0/1; segmented controls report a zero-based index."),
        ConfigurationParameters = [ActionParameter.WidgetTarget("widgetId", new WidgetTargetOptions { Label = TextCatalog.Reference("Target widget"), Required = true, AllowSelf = false }),
            ActionParameter.Text("elementId", label: TextCatalog.Reference("Element ID (XML id)"), required: true)],
        PayloadParameters = new[] { ActionParameter.Text("widgetId", label: TextCatalog.Reference("Target widget ID"), required: true),
            ActionParameter.Text("elementId", label: TextCatalog.Reference("Element ID (XML id)"), required: true) }.Concat(p.Key is "adjust" or "change"
                ? new[] { ActionParameter.Number("value", label: TextCatalog.Reference("Input value"), required: true),
                    ActionParameter.Number("level", label: TextCatalog.Reference("Level (0–1)"), required: true),
                    ActionParameter.Text("key", label: TextCatalog.Reference("Display data key"), required: true) } : p.Key is "position-start" or "position-changing" or "position-end" ? new[] {
                    ActionParameter.Number("startX", label: TextCatalog.Reference("Start X value"), required: true),
                    ActionParameter.Number("startY", label: TextCatalog.Reference("Start Y value"), required: true),
                    ActionParameter.Number("previousX", label: TextCatalog.Reference("Previous X value"), required: true),
                    ActionParameter.Number("previousY", label: TextCatalog.Reference("Previous Y value"), required: true),
                    ActionParameter.Number("x", label: TextCatalog.Reference("X value"), required: true),
                    ActionParameter.Number("y", label: TextCatalog.Reference("Y value"), required: true),
                    ActionParameter.Number("levelX", label: TextCatalog.Reference("X level (0–1)"), required: true),
                    ActionParameter.Number("levelY", label: TextCatalog.Reference("Y level (0–1)"), required: true),
                    ActionParameter.Text("keyX", label: TextCatalog.Reference("X data key"), required: true),
                    ActionParameter.Text("keyY", label: TextCatalog.Reference("Y data key"), required: true) } : []).ToArray()
    })).ToArray();
    public static string ControlEventId(string name) => name.StartsWith("position-", StringComparison.Ordinal) ? "trackpad-" + name[9..] : "element-" + name;
    private static bool IsControlEvent(string? id) => id != null && ControlNames.Keys.Any(name => ControlEventId(name) == id);
    public static JsonElement Empty => JsonSerializer.SerializeToElement(Array.Empty<object>());
    public static JsonElement MoveFlow(JsonElement value, string triggerId, int offset)
    {
        if (offset is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(offset));
        var flows = JsonNode.Parse(value.GetRawText())!.AsArray();
        var matches = flows.Select((flow, index) => (flow, index)).Where(p => p.flow?["triggerId"]?.ToString() == triggerId).ToArray();
        if (matches.Length != 1) return value;
        var from = matches[0].index; var to = from + offset;
        if (to < 0 || to >= flows.Count) return value;
        var item = flows[from]; flows.RemoveAt(from); flows.Insert(to, item);
        return JsonSerializer.SerializeToElement(flows);
    }
    public static string FlowLabel(JsonElement flow, int index)
    {
        var name = flow.TryGetProperty("triggerLabel", out var label) && label.ValueKind == JsonValueKind.String ? label.GetString() : null;
        if (flow.TryGetProperty("event", out var binding))
        {
            var id = binding.TryGetProperty("eventId", out var eventId) ? eventId.GetString() ?? "Event" : "Event";
            name = binding.TryGetProperty("eventName", out var eventName) && eventName.ValueKind == JsonValueKind.String ? eventName.GetString() : id;
        }
        return $"{index + 1}. {name ?? "Event"}";
    }
    private static void DescribeEvent(JsonObject binding)
    {
        var provider = binding["providerId"]?.ToString(); var id = binding["eventId"]?.ToString() ?? "";
        var parameters = (binding["parameters"] as JsonArray)?.OfType<JsonObject>().ToArray() ?? [];
        string Parameter(string key) => parameters.FirstOrDefault(p => p["name"]?.ToString() == key)?["value"]?.ToString() ?? "";
        string? name = null;
        if (provider == CustomButtonIntegration.PluginId)
        {
            name = Names.GetValueOrDefault(id) ?? ControlNames.FirstOrDefault(p => ControlEventId(p.Key) == id).Value;
            if (name != null && Parameter("elementId") is { Length: > 0 } element) name += " — " + element;
        }
        else if (provider == "macro-deck" && id == "variable-changed")
            name = "Variable Changed — " + (Parameter("variable") is { Length: > 0 } variable ? variable : "Select variable");
        if (name != null) binding["eventName"] = name;
    }
    public static JsonElement AddDisplayTick(JsonElement value, string? widgetId)
        => AddElementEvent(value, widgetId, Tick, null, "");

    public static JsonElement AddElementEvent(JsonElement value, string? widgetId, string eventId, string? elementId, string layout)
    {
        if (string.IsNullOrWhiteSpace(widgetId)) throw new FormatException("Save the button before adding a display update event.");
        var definition = Definitions.SingleOrDefault(d => d.Id == eventId) ?? throw new FormatException("Invalid event type.");
        var parameters = new JsonArray(new JsonObject { ["name"] = "widgetId", ["type"] = "widget-target",
            ["label"] = JsonSerializer.SerializeToNode(TextCatalog.Reference("Target widget")), ["required"] = true,
            ["optionsSourceId"] = "macrodeck.widgets", ["allowSelf"] = false, ["value"] = widgetId, ["operator"] = "==" });
        if (IsControlEvent(eventId))
        {
            if (!InputEvents(layout, elementId).Any(d => d.Id == eventId)) throw new FormatException("Select an input component supported by this event.");
            parameters.Add(new JsonObject { ["name"] = "elementId", ["type"] = "string",
                ["label"] = JsonSerializer.SerializeToNode(TextCatalog.Reference("Element ID (XML id)")), ["required"] = true,
                ["value"] = elementId, ["operator"] = "==" });
        }
        else if (!string.IsNullOrEmpty(elementId)) throw new FormatException("This event targets the whole button.");
        return AddFlow(value, widgetId, new JsonObject { ["providerId"] = CustomButtonIntegration.PluginId,
            ["eventId"] = eventId, ["eventName"] = JsonSerializer.SerializeToNode(definition.Name), ["parameters"] = parameters });
    }

    // Captured from the standard beta.11 event editor. Leave optional filters unset;
    // the user selects the watched variable in the native event configuration.
    public static JsonElement AddVariableChanged(JsonElement value, string? widgetId) => AddFlow(value, widgetId, new JsonObject
    {
        ["providerId"] = "macro-deck", ["eventId"] = "variable-changed", ["eventName"] = "Variable Changed",
        ["parameters"] = new JsonArray(
            new JsonObject { ["name"] = "variable", ["type"] = "autocomplete", ["label"] = "Variable",
                ["description"] = "Which variable to watch.", ["required"] = true, ["optionsSourceId"] = "macrodeck.variables", ["value"] = null },
            new JsonObject { ["name"] = "value", ["type"] = "string", ["label"] = "Changed to",
                ["description"] = "Optional. Only run when the new value matches this.", ["required"] = false, ["value"] = null },
            new JsonObject { ["name"] = "previousValue", ["type"] = "string", ["label"] = "Changed from",
                ["description"] = "Optional. Only run when the previous value matches this.", ["required"] = false, ["value"] = null })
    });

    public static IReadOnlyList<EventDefinition> InputEvents(string layout, string? elementId)
    {
        if (string.IsNullOrEmpty(elementId)) return Definitions.Where(d => Names.ContainsKey(d.Id) && d.Id is not (Tick or Activated)).ToArray();
        var target = InputTargets(layout).FirstOrDefault(t => t.Id == elementId);
        return target == null ? [] : Definitions.Where(d => target.Kind == "trackpad" ? d.Id is "trackpad-start" or "trackpad-changing" or "trackpad-end" : target.Kind is "slider" or "dial"
            ? d.Id is "element-adjust" or "element-change" : target.Kind is "toggle" or "segmented" ? d.Id == "element-change" : d.Id is "element-press" or "element-long-press" or "element-press-start" or "element-press-end").ToArray();
    }

    internal static JsonElement AddFlow(JsonElement value, string? widgetId, JsonObject binding)
    {
        var normalized = Normalize(value, widgetId);
        var flows = JsonNode.Parse(normalized.GetRawText())!.AsArray();
        static string[] Conditions(JsonObject e) => (e["parameters"] as JsonArray ?? []).OfType<JsonObject>()
            .Select(p => new JsonArray(p["name"]?.DeepClone(), JsonValue.Create(p["operator"]?.ToString() ?? "=="), p["value"]?.DeepClone()).ToJsonString()).Order(StringComparer.Ordinal).ToArray();
        if (flows.OfType<JsonObject>().Any(f => f["event"] is JsonObject e
            && e["providerId"]?.ToString() == binding["providerId"]?.ToString() && e["eventId"]?.ToString() == binding["eventId"]?.ToString()
            && Conditions(e).SequenceEqual(Conditions(binding)))) return normalized;
        if (flows.Count >= 64) throw new FormatException("You can add up to 64 event flows.");
        flows.Add(new JsonObject { ["triggerId"] = Guid.NewGuid().ToString(), ["triggerType"] = "onEvent", ["event"] = binding, ["children"] = new JsonArray() });
        return Normalize(JsonSerializer.SerializeToElement(flows), widgetId);
    }

    public sealed record InputTarget(string Id, string Kind);
    public static IReadOnlyList<InputTarget> InputTargets(string layout)
    {
        try { return XDocument.Parse(layout).Descendants().Where(e => CanInteract(e) && e.Attribute("id") != null
            && !e.Ancestors("segmented").Any() && e.Name.LocalName is "stack" or "layer" or "slider" or "dial" or "toggle" or "segmented" or "trackpad")
            .Select(e => new InputTarget((string)e.Attribute("id")!, e.Name.LocalName is "slider" or "dial" or "toggle" or "segmented" or "trackpad" ? e.Name.LocalName : "press")).Distinct().ToArray(); }
        catch (System.Xml.XmlException) { return []; }
    }
    private static bool CanInteract(XElement e) => (string?)e.Attribute("interactive") == "true"
        || e.Elements("style").Any(s => (string?)s.Attribute("interactive") == "true" || ((string?)s.Attribute("interactive"))?.Contains("{{", StringComparison.Ordinal) == true)
        || ((string?)e.Attribute("interactive"))?.Contains("{{", StringComparison.Ordinal) == true;
    private static JsonObject Binding(string id, string widgetId) => new()
    {
        ["providerId"] = CustomButtonIntegration.PluginId, ["eventId"] = id, ["eventName"] = JsonSerializer.SerializeToNode(TextCatalog.Reference(Names[id])),
        ["parameters"] = new JsonArray(new JsonObject { ["name"] = "widgetId", ["value"] = widgetId, ["operator"] = "==" })
    };
    public static JsonElement Normalize(JsonElement value, string? widgetId)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > 64 || value.GetRawText().Length > 200000) throw new FormatException("Actions must be an array with up to 64 flows and 200000 characters.");
        var flows = JsonNode.Parse(value.GetRawText())!.AsArray();
        foreach (var item in flows)
        {
            if (item is not JsonObject flow) throw new FormatException("Invalid action flow format.");
            var trigger = flow["triggerType"]?.GetValue<string>() ?? "";
            if (TriggerEvents.TryGetValue(trigger, out var id))
            {
                if (string.IsNullOrWhiteSpace(widgetId)) throw new FormatException("Save the button before configuring actions.");
                flow["triggerType"] = "onEvent"; flow["triggerLabel"] = Names[id]; flow["event"] = Binding(id, widgetId);
            }
            else if (trigger != "onEvent") throw new FormatException("This button supports short press, long press, touch start/end and Events triggers.");
            // Event filters compare payload values. Resolve the widget-target sentinel here,
            // rather than depending on the host to interpret it in a custom event condition.
            if (flow["event"] is JsonObject binding && binding["providerId"]?.ToString() == CustomButtonIntegration.PluginId
                && binding["parameters"] is JsonArray parameters)
            {
                foreach (var parameter in parameters.OfType<JsonObject>())
                {
                    if (parameter["name"]?.ToString() == "widgetId" && parameter["value"]?.ToString() == "$self")
                    {
                        if (string.IsNullOrWhiteSpace(widgetId)) throw new FormatException("Save the button before configuring actions.");
                        parameter["value"] = widgetId;
                    }
                }
            }
            if (flow["event"] is JsonObject namedBinding) DescribeEvent(namedBinding);
        }
        return JsonSerializer.SerializeToElement(flows);
    }

    public static string ElementTargetHelp(JsonElement flows, string layout, string? widgetId)
    {
        XDocument document;
        try { document = XDocument.Parse(layout); }
        catch (System.Xml.XmlException) { return ""; }
        var elements = document.Descendants().Where(e => e.Attribute("id") != null).ToArray();
        var pressTargets = elements.Where(e => (e.Name.LocalName is "stack" or "layer") && CanInteract(e)).ToArray();
        var sliders = elements.Where(e => (e.Name.LocalName is "slider" or "dial" or "toggle" or "segmented" or "trackpad") && CanInteract(e) && !e.Ancestors("segmented").Any()).ToArray();
        var notes = new List<string>();
        if (pressTargets.Length > 0) notes.Add("Press target IDs: " + string.Join(", ", pressTargets.Select(e => (string)e.Attribute("id")!)));
        if (sliders.Length > 0) notes.Add("Value control target IDs: " + string.Join(", ", sliders.Select(e => (string)e.Attribute("id")!)));
        foreach (var flow in flows.EnumerateArray())
        {
            if (!flow.TryGetProperty("event", out var binding) || !binding.TryGetProperty("providerId", out var provider)
                || provider.GetString() != CustomButtonIntegration.PluginId || !binding.TryGetProperty("eventId", out var eventId)
                || !binding.TryGetProperty("parameters", out var parameters) || parameters.ValueKind != JsonValueKind.Array) continue;
            string? Value(string name)
            {
                var parameter = parameters.EnumerateArray().FirstOrDefault(p => p.TryGetProperty("name", out var n) && n.GetString() == name);
                return parameter.ValueKind == JsonValueKind.Object && parameter.TryGetProperty("value", out var value)
                    && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            }
            if (Value("widgetId") is { } target && target != "$self" && target != widgetId) continue;
            var id = Value("elementId");
            if (string.IsNullOrEmpty(id) || !IsControlEvent(eventId.GetString())) continue;
            var candidates = eventId.GetString() == "element-adjust" ? sliders.Where(e => e.Name.LocalName is "slider" or "dial").ToArray()
                : eventId.GetString() == "element-change" ? sliders.Where(e => e.Name.LocalName != "trackpad").ToArray()
                : eventId.GetString() is "trackpad-start" or "trackpad-changing" or "trackpad-end" ? sliders.Where(e => e.Name.LocalName == "trackpad").ToArray() : pressTargets;
            if (candidates.Any(e => (string?)e.Attribute("id") == id)) continue;
            var parent = elements.FirstOrDefault(e => (string?)e.Attribute("id") == id)?.Ancestors().FirstOrDefault(candidates.Contains);
            notes.Add(parent == null ? $"'{id}' is not a target for {eventId.GetString()}." : $"'{id}' is not interactive. Use its parent ID '{(string?)parent.Attribute("id")}'.");
        }
        return string.Join("\n", notes.Distinct());
    }
}

