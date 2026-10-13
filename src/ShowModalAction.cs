using System.Text.Json;
using System.Xml;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Ui;
using MacroDeck.Sdk.Decks;

namespace Ziopuzzle.CustomButton;

// A selected device resolves to its current client at execution time.
public sealed class ShowModalAction(Func<IDeckNavigator?>? navigator = null) : IActionDefinition
{
    public const string ViewId = "custom-button-modal";
    public const string SampleLayout = "<scroll id=\"content\" direction=\"vertical\" padding=\"4%\">\n  <text id=\"text\" size=\"8%\">Example</text>\n</scroll>";
    public string Id => "show-modal";
    public LocalizedText Name => TextCatalog.Reference("Show modal");
    public LocalizedText Description => TextCatalog.Reference("Display XML and channel data in a client dialog. Select a destination device when running from an event.");
    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Text("title", label: TextCatalog.Reference("Modal title"), defaultValue: "Custom Button modal"),
        ActionParameter.DynamicChoice("deviceId", label: TextCatalog.Reference("Destination device"),
            description: TextCatalog.Reference("Optional for a standard button press. Required for Custom Button events. An offline device fails without falling back to another device."), optionsSourceId: "macrodeck.devices"),
        ActionParameter.Text("channel", label: TextCatalog.Reference("Channel"), defaultValue: "demo", required: true),
        ActionParameter.WidgetTarget("eventWidgetId", new WidgetTargetOptions { Label = TextCatalog.Reference("Event target widget"), Description = TextCatalog.Reference("Dialog element events use this widget ID. Defaults to the button running this action."), AllowSelf = true }),
        LiteralCode("layout", "xml", "Drawing XML", SampleLayout),
        LiteralCode("initialValues", "json", "Initial data JSON", "{}")
    ];
    private static ActionParameter LiteralCode(string name, string language, string label, string value) => new()
    {
        Name = name, Type = ActionParameterType.Code, Language = language,
        Description = name == "initialValues" ? TextCatalog.Reference("Fallback values for this modal only. Use {} to disable. Existing Channel values take priority; this does not reset the Channel.") : null,
        Label = TextCatalog.Reference(label), DefaultValue = value, Required = true, LiteralOnly = true
    };
    public IActionExecutor CreateExecutor() => new Executor(navigator);
    private sealed class Executor(Func<IDeckNavigator?>? navigator) : IActionExecutor
    {
        public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (context.Ui is null)
                return ActionResult.Failed(ActionErrorCodes.Unavailable, TextCatalog.Reference("The host did not provide the dialog API for this action."));
            var clientId = context.OriginClientId;
            var device = UpdateValueAction.Parameter(context, "deviceId").Trim();
            if (device.Length > 0)
            {
                if (!Guid.TryParse(device, out var deviceId)) return Failure("Select a valid destination device.");
                clientId = navigator?.Invoke()?.GetClients().FirstOrDefault(c => Guid.TryParse(c.DeviceId, out var id) && id == deviceId)?.ClientId;
                if (string.IsNullOrWhiteSpace(clientId))
                    return ActionResult.Failed(ActionErrorCodes.Unavailable, TextCatalog.Reference("The selected destination device is not connected. No other device was used."));
            }
            if (string.IsNullOrWhiteSpace(clientId))
                return Failure("Select a Destination device for this event. There is no originating client to display the dialog on.");
            try
            {
                var channel = UpdateValueAction.Parameter(context, "channel").Trim();
                var layout = UpdateValueAction.Parameter(context, "layout");
                var initial = UpdateValueAction.Parameter(context, "initialValues");
                var eventWidgetId = UpdateValueAction.Parameter(context, "eventWidgetId").Trim();
                if (eventWidgetId is "" or "$self") eventWidgetId = context.OwnerWidgetId ?? "";
                DataHub.ValidateName(channel);
                _ = new LayoutRenderer(layout);
                _ = DataHub.ParseValues(initial);
                var opened = await context.Ui.ShowModalAsync(clientId, new ModalDefinition
                {
                    ViewId = ViewId,
                    Title = TextCatalog.Reference(UpdateValueAction.Parameter(context, "title")),
                    Data = new Dictionary<string, JsonElement>
                    {
                        ["channel"] = JsonSerializer.SerializeToElement(channel),
                        ["eventWidgetId"] = JsonSerializer.SerializeToElement(eventWidgetId),
                        ["layout"] = JsonSerializer.SerializeToElement(layout),
                        ["initialValues"] = JsonSerializer.SerializeToElement(initial)
                    }
                }, context.CancellationToken);
                return opened ? ActionResult.Success() : ActionResult.Failed(ActionErrorCodes.Unavailable, TextCatalog.Reference("The host could not open the modal on the destination client. The device must support graphical dialogs."));
            }
            catch (Exception e) when (e is FormatException or JsonException or ArgumentException or XmlException)
            { return Failure(e.Message); }
        }
        private static ActionResult Failure(string message) => ActionResult.Failed(ActionErrorCodes.InvalidParameter, TextCatalog.Reference(message));
    }
}
