using System.Text.Json;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Variables;
using MacroDeck.Sdk.Widgets;

namespace Ziopuzzle.CustomButton;

internal static class TransferData
{
    internal static ActionParameter Channel() => ActionParameter.Text("channel", label: "Source channel", defaultValue: "demo", required: true,
        description: "Read initial JSON from the source widget, overridden by current Channel values.");
    internal static ActionParameter InitialWidget() => ActionParameter.WidgetTarget("sourceWidgetId", new WidgetTargetOptions
    {
        Label = "Initial JSON from widget",
        Description = "Optional: use this Custom Button's saved initial JSON. Empty uses the widget running this action, only when its Channel matches. Select a source explicitly when copying another widget's data.",
        AllowSelf = true
    });
    internal static ActionParameter Key() => ActionParameter.Text("key", label: "Source data name", defaultValue: "value", required: true);
    internal static string Read(ActionExecutionContext context, string name) => UpdateValueAction.Parameter(context, name).Trim();
    internal static async Task<IReadOnlyDictionary<string, JsonElement>> Snapshot(DataHub hub, ActionExecutionContext context,
        Func<string, CancellationToken, Task<WidgetDataSnapshot?>>? readWidget)
    {
        var channel = Read(context, "channel");
        DataHub.ValidateName(channel);
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var source = Read(context, "sourceWidgetId");
        bool explicitSource = source.Length > 0;
        if (source is "" or "$self") source = context.OwnerWidgetId ?? "";
        var saved = source.Length > 0 && readWidget != null ? await readWidget(source, context.CancellationToken) : null;
        context.CancellationToken.ThrowIfCancellationRequested();
        if (saved?.WidgetTypeId == CustomButtonIntegration.TypeId)
        {
            var settings = ButtonSettings.Read(saved.Data);
            if (settings.Channel == channel) values = DataHub.ParseValues(settings.InitialValues);
            else if (explicitSource) throw new FormatException("The selected source widget's Channel does not match Source channel.");
        }
        else if (explicitSource) throw new FormatException("Cannot read initial JSON from the selected Custom Button. Save it first and check the source widget.");
        foreach (var pair in hub.Snapshot(channel).Values) values[pair.Key] = pair.Value;
        if (values.Count == 0) throw new FormatException($"Channel '{channel}' has no display data. Select the widget containing its initial JSON or set Channel data first.");
        return values;
    }
    internal static async Task<JsonElement> Value(DataHub hub, ActionExecutionContext context,
        Func<string, CancellationToken, Task<WidgetDataSnapshot?>>? readWidget, bool detailedTarget = false)
    {
        var key = Read(context, "key");
        if (detailedTarget) return DataTarget.Read(await Snapshot(hub, context, readWidget), key);
        DataHub.ValidateName(key);
        if (!(await Snapshot(hub, context, readWidget)).TryGetValue(key, out var value))
            throw new FormatException($"No display value named '{key}' exists in initial JSON or Channel data.");
        return value;
    }
}

internal sealed class TransferExecutor(Func<ActionExecutionContext, Task> transfer) : IActionExecutor
{
    public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        try { await transfer(context); return ActionResult.Success(); }
        catch (Exception e) when (e is FormatException or JsonException or ArgumentException)
        { return ActionResult.Failed(ActionErrorCodes.InvalidParameter, e.Message); }
    }
}

public sealed class CopyDisplayValueAction(DataHub hub, Func<string, CancellationToken, Task<WidgetDataSnapshot?>>? readWidget = null) : IActionDefinition
{
    public string Id => "copy-value";
    public LocalizedText Name => "Copy display value";
    public LocalizedText Description => "Copy one display value to a channel, preserving its JSON type. Other destination values are kept.";
    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        TransferData.Channel(), TransferData.InitialWidget(), TransferData.Key(),
        ActionParameter.Text("destinationChannel", label: "Destination channel", required: true,
            description: "Must match the Channel of the receiving widget or modal. This copies data, not XML or initial JSON. Check live values in Diagnostics > Refresh diagnostics."),
        ActionParameter.Text("destinationKey", label: "Destination data name", description: "Leave empty to use the source data name.")
    ];
    public IActionExecutor CreateExecutor() => new TransferExecutor(async context =>
    {
        var value = await TransferData.Value(hub, context, readWidget);
        var key = TransferData.Read(context, "destinationKey");
        if (key.Length == 0) key = TransferData.Read(context, "key");
        hub.Update(TransferData.Read(context, "destinationChannel"), new Dictionary<string, JsonElement> { [key] = value });
    });
}

public sealed class CopyDisplayDataAction(DataHub hub, Func<string, CancellationToken, Task<WidgetDataSnapshot?>>? readWidget = null) : IActionDefinition
{
    public string Id => "copy-data";
    public LocalizedText Name => "Copy display data";
    public LocalizedText Description => "Copy a snapshot of all display values to another channel. History is not copied.";
    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        TransferData.Channel(), TransferData.InitialWidget(),
        ActionParameter.Text("destinationChannel", label: "Destination channel", required: true,
            description: "Must match the Channel of the receiving widget or modal. This copies data, not XML or initial JSON. Check live values in Diagnostics > Refresh diagnostics."),
        ActionParameter.Choice("mode", [new() { Value = "merge", Label = "Merge (keep other destination values)" },
            new() { Value = "replace", Label = "Replace all destination values" }], label: "Copy mode", defaultValue: "merge")
    ];
    public IActionExecutor CreateExecutor() => new TransferExecutor(async context =>
    {
        var mode = TransferData.Read(context, "mode");
        if (mode is not ("" or "merge" or "replace")) throw new FormatException("Unknown copy mode.");
        var values = await TransferData.Snapshot(hub, context, readWidget);
        hub.Update(TransferData.Read(context, "destinationChannel"), values, replace: mode == "replace");
    });
}

public sealed class WriteDisplayVariableAction(DataHub hub, Func<IUserVariableApi?> variables, Func<string, CancellationToken, Task<WidgetDataSnapshot?>>? readWidget = null) : IActionDefinition
{
    public string Id => "write-variable";
    public LocalizedText Name => "Write display value to variable";
    public LocalizedText Description => "Write a display value to an existing writable Macro Deck variable. The host converts it to the destination variable's type.";
    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        TransferData.Channel(), TransferData.InitialWidget(),
        ActionParameter.Text("key", label: "Source target", required: true, description: DataTarget.Help),
        ActionParameter.Autocomplete("variable", label: "Destination variable", optionsSourceId: "macrodeck.user-variables", required: true,
            description: "Select a variable name, not a value binding. Create the variable in Macro Deck first."),
        ActionParameter.Choice("scope", [new() { Value = "owner", Label = "Prefer this widget's variable; otherwise use global" },
            new() { Value = "global", Label = "Use the global variable (ignore this widget's variable)" }],
            label: "Which variable to write", defaultValue: "owner",
            description: "Local variables are created using Variables in the action editor; they are not Custom Button JSON data. Neither option searches other widgets. Both options set the value. This only selects the destination when local and global variables have the same name. 'This widget' is the widget running this action, not the source Channel. If there is no local variable, both options use the global variable.")
    ];
    public IActionExecutor CreateExecutor() => new Executor(hub, variables, readWidget);
    private sealed class Executor(DataHub hub, Func<IUserVariableApi?> variables, Func<string, CancellationToken, Task<WidgetDataSnapshot?>>? readWidget) : IActionExecutor
    {
        public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            try
            {
                var value = await TransferData.Value(hub, context, readWidget, detailedTarget: true);
                var name = TransferData.Read(context, "variable");
                if (name.Length == 0) throw new FormatException("Select a destination variable.");
                var scope = TransferData.Read(context, "scope");
                if (scope is not ("" or "owner" or "global")) throw new FormatException("Unknown variable scope.");
                var api = variables();
                if (api == null) return ActionResult.Failed(ActionErrorCodes.Unavailable, "The variable service is unavailable.");
                // The host API accepts strings and applies the destination's type and precision.
                var text = value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.Null => "",
                    _ => value.GetRawText()
                };
                var result = await api.ApplyAsync(name, scope == "global" ? null : context.OwnerWidgetId,
                    UserVariableOperation.Set, text, context.CancellationToken);
                if (result.Status == UserVariableWriteStatus.Applied) return ActionResult.Success();
                var code = result.Status switch
                {
                    UserVariableWriteStatus.NotFound => ActionErrorCodes.NotFound,
                    UserVariableWriteStatus.NotEditable => ActionErrorCodes.PermissionDenied,
                    UserVariableWriteStatus.Unavailable => ActionErrorCodes.NotConnected,
                    _ => ActionErrorCodes.InvalidParameter
                };
                return ActionResult.Failed(code, result.Message ?? $"Variable write failed: {result.Status}.");
            }
            catch (Exception e) when (e is FormatException or JsonException or ArgumentException)
            { return ActionResult.Failed(ActionErrorCodes.InvalidParameter, e.Message); }
        }
    }
}
