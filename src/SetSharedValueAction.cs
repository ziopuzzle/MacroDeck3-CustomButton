using System.Text.Json;
using MacroDeck.Localization;
using MacroDeck.Plugin.Hosting.Transport;
using MacroDeck.Sdk.Actions;

namespace Ziopuzzle.CustomButton;

internal sealed class SetSharedValueAction(Func<string, JsonElement, CancellationToken, Task<ActionResult>> save)
    : IActionDefinition, IActionExecutor
{
    public string Id => "set-shared-value";
    public LocalizedText Name => "Set shared display value";
    public LocalizedText Description => "Save one shared default for widgets and modals on every channel. Set up Shared display values in Integrations > Custom Button before first use.";
    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Text("key", label: "Shared data name", required: true,
            description: "A top-level key from Shared values (JSON), such as themeAccent. A new name adds a key."),
        ActionParameter.Text("value", label: "Value (supports variable bindings)"),
        DataTarget.ValueType()
    ];
    public IActionExecutor CreateExecutor() => this;
    public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        try
        {
            var name = UpdateValueAction.Parameter(context, "key").Trim();
            DataHub.ValidateName(name);
            return await save(name, DataTarget.Value(context), context.CancellationToken);
        }
        catch (Exception e) when (e is FormatException or JsonException)
        { return ActionResult.Failed(ActionErrorCodes.InvalidParameter, e.Message); }
        catch (Exception e) when (e is HostInvocationException or IOException or InvalidOperationException)
        { return ActionResult.Failed(ActionErrorCodes.ProviderError, "Could not update shared display settings: " + e.Message); }
    }
}
