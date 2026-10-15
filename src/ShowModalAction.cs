using System.Text.Json;
using System.Xml;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Ui;
using MacroDeck.Sdk.Decks;

namespace Ziopuzzle.CustomButton;

// A selected device resolves to its current client at execution time.
public sealed class ShowModalAction(Func<IDeckNavigator?>? navigator = null, Func<MacroDeck.Sdk.Widgets.IWidgetApi?>? widgets = null,
    Func<MacroDeck.Sdk.Events.IEventPublisher?>? events = null) : IUiConfigurableActionDefinition
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> waiting = new();
    internal void SessionOpened(string id) => waiting.TryUpdate(id, true, false);
    public const string ViewId = "custom-button-modal";
    public const string SampleLayout = "<scroll id=\"content\" direction=\"vertical\" padding=\"4%\" gap=\"3%\">\n  <text id=\"text\" size=\"8%\">Example</text>\n  <stack id=\"close\" closeModal=\"true\" background=\"#335577\" padding=\"3%\" align=\"center\">\n    <text id=\"closeLabel\" size=\"7%\">Close</text>\n  </stack>\n</scroll>";
    public string Id => "show-modal";
    public LocalizedText Name => TextCatalog.Reference("Show modal");
    public LocalizedText Description => TextCatalog.Reference("Display XML and channel data in a client dialog. Select a destination device when running from an event.");
    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Text("title", label: TextCatalog.Reference("Modal title"), defaultValue: "Custom Button modal"),
        ActionParameter.DynamicChoice("deviceId", label: TextCatalog.Reference("Destination device"),
            description: TextCatalog.Reference("Optional for a standard button press. Required for Custom Button events. An offline device fails without falling back to another device."), optionsSourceId: "macrodeck.devices"),
        ActionParameter.Text("channel", label: TextCatalog.Reference("Channel"), defaultValue: "demo", required: true),
        ActionParameter.Choice("waitForClose", [new() { Value = "false", Label = TextCatalog.Reference("Continue immediately (no close event)") }, new() { Value = "true", Label = TextCatalog.Reference("Wait until closed (emit Modal closed)") }],
            label: TextCatalog.Reference("After opening"), description: TextCatalog.Reference("To trigger Modal closed, select Wait until closed. This pauses subsequent actions until the modal closes. Adding the event alone does not enable it."), defaultValue: "false"),
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
    public IActionExecutor CreateExecutor() => new Executor(navigator, events, waiting);
    public Task<IUiSession?> CreateConfigurationSessionAsync(ActionConfigurationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IUiSession?>(new ModalConfigurationSession(request, widgets: widgets));
    }
    private sealed class Executor(Func<IDeckNavigator?>? navigator, Func<MacroDeck.Sdk.Events.IEventPublisher?>? events,
        System.Collections.Concurrent.ConcurrentDictionary<string, bool> waiting) : IActionExecutor
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
                var waitText = UpdateValueAction.Parameter(context, "waitForClose").Trim();
                if (waitText.Length > 0 && !bool.TryParse(waitText, out _)) return Failure("After opening must be true or false.");
                var wait = bool.TryParse(waitText, out var flag) && flag;
                if (wait && string.IsNullOrWhiteSpace(eventWidgetId)) return Failure("Select an Event target widget to publish Modal closed.");
                var waitId = Guid.NewGuid().ToString("N");
                var modal = new ModalDefinition
                {
                    ViewId = ViewId,
                    Title = TextCatalog.Reference(UpdateValueAction.Parameter(context, "title")),
                    Data = new Dictionary<string, JsonElement>
                    {
                        ["channel"] = JsonSerializer.SerializeToElement(channel),
                        ["eventWidgetId"] = JsonSerializer.SerializeToElement(eventWidgetId),
                        ["layout"] = JsonSerializer.SerializeToElement(layout),
                        ["initialValues"] = JsonSerializer.SerializeToElement(initial),
                        ["closeWaitId"] = JsonSerializer.SerializeToElement(waitId)
                    }
                };
                if (wait)
                {
                    waiting[waitId] = false;
                    var cancelled = true;
                    try
                    {
                        var result = await context.Ui.ShowModalAsync<string>(clientId, modal, context.CancellationToken);
                        cancelled = result.Cancelled;
                        context.CancellationToken.ThrowIfCancellationRequested();
                        return waiting.GetValueOrDefault(waitId) ? ActionResult.Success()
                            : ActionResult.Failed(ActionErrorCodes.Unavailable, TextCatalog.Reference("The modal session did not open."));
                    }
                    finally
                    {
                        if (waiting.TryRemove(waitId, out var didOpen) && didOpen)
                            events?.Invoke()?.Publish(ButtonEvents.ModalClosed, new Dictionary<string, object?> {
                                ["widgetId"] = eventWidgetId, ["channel"] = channel, ["cancelled"] = cancelled });
                    }
                }
                var opened = await context.Ui.ShowModalAsync(clientId, modal, context.CancellationToken);
                return opened ? ActionResult.Success() : ActionResult.Failed(ActionErrorCodes.Unavailable, TextCatalog.Reference("The host could not open the modal on the destination client. The device must support graphical dialogs."));
            }
            catch (Exception e) when (e is FormatException or JsonException or ArgumentException or XmlException)
            { return Failure(e.Message); }
        }
        private static ActionResult Failure(string message) => ActionResult.Failed(ActionErrorCodes.InvalidParameter, TextCatalog.Reference(message));
    }
}
