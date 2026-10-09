using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ziopuzzle.CustomButton;

public sealed record ButtonSettings(string Channel, string Layout, string InitialValues)
{
    public DesignSettings? Design { get; init; }
    public bool WholeButtonInteraction { get; init; } = true;
    public JsonElement Flows { get; init; } = ButtonEvents.Empty;
    public string ConfigurationWidgetId { get; init; } = "";
    public const string DefaultLayout = "<stack id=\"container\"></stack>";
    public static ButtonSettings Default { get; } = new("demo", DefaultLayout, "{}");
    // XML is the saved design. Old templateId values remain accepted by the schema but do not
    // describe hand-edited XML reliably, so new buttons no longer write this redundant field.
    // Empty only until the host assigns a widget ID; configuration persists the resolved ID.
    public static string DefaultData => JsonSerializer.Serialize(new { channel = "",
        layout = Default.Layout, initialValues = Default.InitialValues, designPreset = "xml", flows = Array.Empty<object>() });
    public static string Schema
    {
        get
        {
            var schema = JsonNode.Parse(BaseSchema)!.AsObject();
            schema["properties"]!["layout"]!["maxLength"] = LayoutLimits.XmlCharacters;
            DesignSettings.AddSchema(schema["properties"]!.AsObject());
            return schema.ToJsonString();
        }
    }
    // Accept obsolete script keys in existing saved data, but never read or execute them.
    private const string BaseSchema = """
        {"type":"object","properties":{
          "channel":{"type":"string","minLength":0,"maxLength":64,"pattern":"^[A-Za-z0-9_.-]*$"},
          "layout":{"type":"string","maxLength":16000},
          "initialValues":{"type":"string","maxLength":16000},
          "wholeButtonInteraction":{"type":"boolean"},
          "configurationWidgetId":{"type":"string","maxLength":256},
          "editorLanguage":{"type":"string","maxLength":64},
          "pressScriptId":{"type":"string","maxLength":256},
          "updateScriptId":{"type":"string","maxLength":256},
          "flows":{"type":"array","maxItems":64,"items":{"type":"object"}}
          ,"templateId":{"type":"string","maxLength":64}
        },"required":["channel","layout","initialValues"],"additionalProperties":false}
        """;
    public static ButtonSettings Read(JsonElement data)
    {
        string Get(string key, string fallback) => data.ValueKind == JsonValueKind.Object && data.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : fallback;
        return new(Get("channel", Default.Channel), Get("layout", Default.Layout), Get("initialValues", Default.InitialValues))
        {
            ConfigurationWidgetId = Get("configurationWidgetId", ""),
            Design = new(data), Flows = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("flows", out var flows) && flows.ValueKind == JsonValueKind.Array ? flows.Clone() : ButtonEvents.Empty,
            WholeButtonInteraction = !(data.ValueKind == JsonValueKind.Object && data.TryGetProperty("wholeButtonInteraction", out var interaction) && interaction.ValueKind == JsonValueKind.False)
        };
    }
}

