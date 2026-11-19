using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;

namespace Ziopuzzle.CustomButton;

public sealed class UpdateValueAction(DataHub hub) : IActionDefinition
{
    public string Id => "set-value";
    public LocalizedText Name => TextCatalog.Reference("Set display value");
    public LocalizedText Description => "Set a Channel value, object property or array item. Other values are kept.";
    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Text("channel", label: TextCatalog.Reference("Channel"), defaultValue: "demo", required: true),
        ActionParameter.Text("key", label: "Target", required: true, description: DataTarget.Help),
        ActionParameter.Text("value", label: TextCatalog.Reference("Value (supports variable bindings)")),
        DataTarget.ValueType()
    ];
    public IActionExecutor CreateExecutor() => new UpdateExecutor(context =>
    {
        var channel = Parameter(context, "channel").Trim(); var key = Parameter(context, "key").Trim();
        var value = DataTarget.Value(context);
        // Old saved actions have no input-format parameter and used literal data names.
        if (!context.Parameters.ContainsKey("valueType") && System.Text.RegularExpressions.Regex.IsMatch(key, @"\A[A-Za-z0-9_.-]{1,64}\z"))
        {
            hub.Update(channel, new Dictionary<string, JsonElement> { [key] = value });
            return;
        }
        hub.Edit(channel, data => DataTarget.Apply(data, key, "set", value));
    });
    internal static string Parameter(ActionExecutionContext context, string name) => Convert.ToString(context.Parameters.GetValueOrDefault(name), CultureInfo.InvariantCulture) ?? "";
}

public sealed partial class UpdateValuesAction(DataHub hub) : IActionDefinition
{
    public string Id => "set-data";
    public LocalizedText Name => TextCatalog.Reference("Set display data from JSON");
    public LocalizedText Description => TextCatalog.Reference("Replace all data on a channel with a JSON object.");
    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Text("channel", label: TextCatalog.Reference("Channel"), defaultValue: "demo", required: true),
        ActionParameter.MultilineText("json", label: TextCatalog.Reference("Data JSON"), defaultValue: "{\"title\":\"CPU\",\"value\":75}", required: true, maxLength: 16000)
    ];
    public IActionExecutor CreateExecutor() => new UpdateExecutor(context => hub.Update(UpdateValueAction.Parameter(context, "channel").Trim(),
        ParseActionData(UpdateValueAction.Parameter(context, "json")), replace: true));

    private static Dictionary<string, JsonElement> ParseActionData(string json)
    {
        try { return DataHub.ParseValues(json); }
        catch (JsonException) when (json.Contains('#'))
        {
            // Macro Deck expands text parameters before execution. A color binding used
            // as a property value can therefore arrive as a bare #RRGGBB token.
            var quoted = StringsOrPropertyColors().Replace(json, match => match.Groups["color"] is { Success: true } color
                ? match.Groups["prefix"].Value + "\"" + color.Value + "\"" : match.Value);
            if (quoted == json) throw;
            return DataHub.ParseValues(quoted);
        }
    }

    // Consume JSON strings intact, including escapes, so color-like text inside a title is never rewritten.
    [GeneratedRegex(""" "(?:[^"\\]|\\.)*" | (?<prefix>:[\x20\t\r\n]*)(?<color>\#(?:[a-fA-F0-9]{8}|[a-fA-F0-9]{6}|[a-fA-F0-9]{4}|[a-fA-F0-9]{3})) (?=[\x20\t\r\n]*[,}]) """, RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex StringsOrPropertyColors();
}

internal sealed class UpdateExecutor(Action<ActionExecutionContext> update) : IActionExecutor
{
    public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        try { update(context); return Task.FromResult(ActionResult.Success()); }
        catch (Exception e) when (e is FormatException or JsonException or ArgumentException)
        { return Task.FromResult(ActionResult.Failed(ActionErrorCodes.InvalidParameter, TextCatalog.Reference(e.Message))); }
    }
}

