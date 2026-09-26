using System.Globalization;
using System.Text.Json;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;

namespace Ziopuzzle.CustomButton;

public sealed class UpdateValueAction(DataHub hub) : IActionDefinition
{
    public string Id => "set-value";
    public LocalizedText Name => TextCatalog.Reference("Set display value");
    public LocalizedText Description => TextCatalog.Reference("Update one value on a channel. The value can be bound to a Macro Deck variable.");
    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Text("channel", label: TextCatalog.Reference("Channel"), defaultValue: "demo", required: true),
        ActionParameter.Text("key", label: TextCatalog.Reference("Data name"), defaultValue: "value", required: true),
        ActionParameter.Text("value", label: TextCatalog.Reference("Value (supports variable bindings)"))
    ];
    public IActionExecutor CreateExecutor() => new UpdateExecutor(context =>
    {
        var channel = Parameter(context, "channel").Trim(); var key = Parameter(context, "key").Trim();
        var value = context.Parameters.GetValueOrDefault("value");
        if (value is JsonElement element) value = element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
        hub.Update(channel, new Dictionary<string, JsonElement> { [key] = JsonSerializer.SerializeToElement(value is null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture)) });
    });
    internal static string Parameter(ActionExecutionContext context, string name) => Convert.ToString(context.Parameters.GetValueOrDefault(name), CultureInfo.InvariantCulture) ?? "";
}

public sealed class UpdateValuesAction(DataHub hub) : IActionDefinition
{
    public string Id => "set-data";
    public LocalizedText Name => TextCatalog.Reference("Set display data from JSON");
    public LocalizedText Description => TextCatalog.Reference("Replace all data on a channel with a JSON object.");
    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Text("channel", label: TextCatalog.Reference("Channel"), defaultValue: "demo", required: true),
        ActionParameter.MultilineText("json", label: TextCatalog.Reference("Data JSON"), defaultValue: "{\"title\":\"CPU\",\"value\":75}", required: true, maxLength: 16000)
    ];
    public IActionExecutor CreateExecutor() => new UpdateExecutor(context => hub.Update(UpdateValueAction.Parameter(context, "channel").Trim(), DataHub.ParseValues(UpdateValueAction.Parameter(context, "json")), replace: true));
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

