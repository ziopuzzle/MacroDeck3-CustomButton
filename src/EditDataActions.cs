using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Variables;

namespace Ziopuzzle.CustomButton;

// Shared action target syntax. Existing exact top-level names take precedence for compatibility.
internal static class DataTarget
{
    internal const string Help = "Examples: volume, settings.volume, playlist[2].title. Array indices start at 0; [-1] is the last item. Use [\"a.b\"] for a literal name containing dots. Host variable bindings are supported.";
    internal static List<string> Parse(string text, IReadOnlyDictionary<string, JsonElement> values)
    {
        if (text.Length == 0 || text.Length > 1024) throw new FormatException("Enter a data target (up to 1024 characters).");
        if (values.ContainsKey(text)) return [text];
        var parts = new List<string>();
        var i = 0;
        void Name()
        {
            var start = i;
            while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] is '_' or '-')) i++;
            if (start == i) throw new FormatException("Expected a data name in the target.");
            parts.Add(text[start..i]);
        }
        void Bracket()
        {
            i++;
            if (i < text.Length && text[i] == '"')
            {
                var start = i++;
                while (i < text.Length && text[i] != '"')
                {
                    if (text[i] == '\\') i++;
                    i++;
                }
                if (i >= text.Length) throw new FormatException("Unclosed quoted name in target.");
                parts.Add(JsonSerializer.Deserialize<string>(text[start..++i])!);
            }
            else
            {
                var start = i;
                if (i < text.Length && text[i] == '-') i++;
                while (i < text.Length && char.IsAsciiDigit(text[i])) i++;
                var number = text[start..i];
                if (!int.TryParse(number, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
                    throw new FormatException("Use an integer array index inside brackets.");
                parts.Add(number);
            }
            if (i >= text.Length || text[i++] != ']') throw new FormatException("Expected ] in target.");
        }
        if (text[0] == '[') Bracket(); else Name();
        while (i < text.Length)
        {
            if (text[i] == '.') { i++; Name(); }
            else if (text[i] == '[') Bracket();
            else throw new FormatException("Use dots for properties and brackets for array indices.");
        }
        DataHub.ValidateName(parts[0]);
        return parts;
    }
    private static int Index(string text, int count)
    {
        if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n))
            throw new FormatException("An array requires an integer index.");
        var index = n < 0 ? (long)count + n : n;
        if (index < 0 || index >= count) throw new FormatException("Array index is out of range (0 is first; -1 is last).");
        return (int)index;
    }
    private static JsonNode? Child(JsonNode? node, string part) => node switch
    {
        JsonObject obj when obj.ContainsKey(part) => obj[part],
        JsonArray array => array[Index(part, array.Count)],
        _ => throw new FormatException($"Target property '{part}' does not exist.")
    };
    internal static JsonElement Read(IReadOnlyDictionary<string, JsonElement> data, string target)
    {
        var parts = Parse(target, data);
        if (!data.TryGetValue(parts[0], out var value)) throw new FormatException($"No live data named '{parts[0]}'. Use Set display data from JSON to initialize it.");
        var node = JsonNode.Parse(value.GetRawText());
        foreach (var part in parts.Skip(1)) node = Child(node, part);
        return JsonSerializer.SerializeToElement(node);
    }
    internal static void Apply(Dictionary<string, JsonElement> data, string target, string operation, JsonElement value, string insertIndex = "")
    {
        var parts = Parse(target, data);
        var key = parts[0];
        if (parts.Count == 1 && operation == "set") { data[key] = value; return; }
        if (!data.TryGetValue(key, out var original)) throw new FormatException($"No live data named '{key}'. Use Set display data from JSON to initialize it.");
        if (parts.Count == 1 && operation == "remove") { data.Remove(key); return; }
        var root = JsonNode.Parse(original.GetRawText());
        var node = root;
        var parentCount = operation == "add" ? parts.Count : parts.Count - 1;
        for (var i = 1; i < parentCount; i++) node = Child(node, parts[i]);
        if (operation == "add")
        {
            if (node is not JsonArray array) throw new FormatException("The target must be an array.");
            var index = array.Count;
            if (insertIndex.Length != 0 && (!int.TryParse(insertIndex, NumberStyles.None, CultureInfo.InvariantCulture, out index) || index > array.Count))
                throw new FormatException("Insert position must be between 0 and the array length; leave empty to append.");
            array.Insert(index, JsonNode.Parse(value.GetRawText()));
        }
        else if (node is JsonObject obj)
        {
            if (operation == "set") obj[parts[^1]] = JsonNode.Parse(value.GetRawText());
            else if (!obj.Remove(parts[^1])) throw new FormatException("The property to remove does not exist.");
        }
        else if (node is JsonArray array)
        {
            var index = Index(parts[^1], array.Count);
            if (operation == "set") array[index] = JsonNode.Parse(value.GetRawText());
            else array.RemoveAt(index);
        }
        else throw new FormatException("The target parent must be an object or array.");
        data[key] = JsonSerializer.SerializeToElement(root);
    }
    internal static ActionParameter ValueType() => ActionParameter.Choice("valueType",
        [new() { Value = "text", Label = "Text / variable value" }, new() { Value = "json", Label = "JSON (number, boolean, object or array)" }],
        label: "Input format", defaultValue: "text");
    internal static JsonElement Value(ActionExecutionContext context)
    {
        var raw = context.Parameters.GetValueOrDefault("value");
        var text = raw is JsonElement element
            ? element.ValueKind == JsonValueKind.String ? element.GetString() ?? "" : element.ToString()
            : Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "";
        if (text.Length > 16000) throw new FormatException("Value must not exceed 16000 characters.");
        var kind = TransferData.Read(context, "valueType");
        if (kind is "" or "text") return JsonSerializer.SerializeToElement(text);
        if (kind != "json") throw new FormatException("Unknown input format.");
        using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 8 });
        return document.RootElement.Clone();
    }
}

public sealed class EditDisplayDataAction : IActionDefinition
{
    private readonly DataHub hub;
    private readonly string operation;
    private readonly Func<IUserVariableApi?> variables;
    public EditDisplayDataAction(DataHub hub, string operation, Func<IUserVariableApi?>? variables = null)
    {
        this.hub = hub; this.operation = operation; this.variables = variables ?? (() => null);
        if (operation is not ("add" or "remove")) throw new ArgumentException("Unknown edit operation.");
        var parameters = new List<ActionParameter>
        {
            ActionParameter.Text("channel", label: "Channel", defaultValue: "demo", required: true),
            ActionParameter.Text("key", label: operation == "add" ? "Target array" : "Target", required: true, description: DataTarget.Help)
        };
        if (operation == "add")
        {
            parameters.Add(ActionParameter.Text("value", label: "Item to insert"));
            parameters.Add(DataTarget.ValueType());
            parameters.Add(ActionParameter.Text("index", label: "Insert before index (optional)",
                description: "Leave empty to add at the end. 0 inserts at the beginning."));
        }
        else parameters.Add(ActionParameter.Autocomplete("variable", label: "Store removed value in variable (optional)",
            optionsSourceId: "macrodeck.user-variables",
            description: "Leave empty to discard. Select an existing writable variable name. This widget's variable takes precedence over global. Failed writes leave the data intact."));
        Parameters = parameters;
    }
    public string Id => operation == "add" ? "add-array-item" : "remove-data-part";
    public LocalizedText Name => operation == "add" ? "Insert array item" : "Remove display value";
    public LocalizedText Description => operation == "add"
        ? "Insert a value into an existing Channel array. Leave the index empty to append."
        : "Remove a Channel value, object property or array item. Optionally save the removed value to a Macro Deck variable.";
    public IReadOnlyList<ActionParameter> Parameters { get; }
    public IActionExecutor CreateExecutor() => new TransferExecutor(async context =>
    {
        if (context.Parameters.ContainsKey("path") || context.Parameters.ContainsKey("selection") || context.Parameters.ContainsKey("sourceWidgetId"))
            throw new FormatException("This action uses the previous experimental settings. Remove and add the action again, then enter the complete target in the single Target field.");
        var channel = TransferData.Read(context, "channel");
        var target = TransferData.Read(context, "key");
        var value = operation == "add" ? DataTarget.Value(context) : JsonSerializer.SerializeToElement<object?>(null);
        var destination = TransferData.Read(context, "variable");
        string? expectedRoot = null;
        string? rootKey = null;
        if (operation == "remove" && destination.Length > 0)
        {
            var snapshot = hub.Read(channel);
            var removed = DataTarget.Read(snapshot, target);
            rootKey = DataTarget.Parse(target, snapshot)[0];
            expectedRoot = snapshot[rootKey].GetRawText();
            var api = variables() ?? throw new FormatException("The variable service is unavailable.");
            var text = removed.ValueKind switch { JsonValueKind.String => removed.GetString(), JsonValueKind.Null => "", _ => removed.GetRawText() };
            var result = await api.ApplyAsync(destination, context.OwnerWidgetId, UserVariableOperation.Set, text, context.CancellationToken);
            if (result.Status != UserVariableWriteStatus.Applied)
                throw new FormatException(result.Message ?? $"Variable write failed: {result.Status}. Data was not removed.");
        }
        context.CancellationToken.ThrowIfCancellationRequested();
        hub.Edit(channel, data =>
        {
            // Never remove a different item if another action edited the source during the host write.
            if (rootKey != null && (!data.TryGetValue(rootKey, out var current) || current.GetRawText() != expectedRoot))
                throw new FormatException("Source data changed while writing the variable. The variable was written, but nothing was removed. Retry after other updates finish.");
            DataTarget.Apply(data, target, operation, value, TransferData.Read(context, "index"));
        });
    });
}
