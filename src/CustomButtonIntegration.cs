using System.Text.Json;
using MacroDeck.Plugin.Hosting.Integrations;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Ui;
using MacroDeck.Sdk.Widgets;
using MacroDeck.Sdk.Events;
using MacroDeck.Ui.Model.Surfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MacroDeck.Plugin.Hosting;

namespace Ziopuzzle.CustomButton;

public sealed class CustomButtonIntegration : IPluginIntegration, IWidgetTypeProvider, IUiProvider, IEventProvider
{
    public const string PluginId = "net.ziopuzzle.custombutton";
    public const string TypeId = PluginId + "::custom-button";
    public DataHub Hub { get; } = new();
    private IIntegrationContext? context;
    private static readonly HttpClient ImageHttp = new();
    private readonly Uri? imageHostUrl;
    private readonly SharedScriptUpdates updates = new();
    private AnimationContinuity continuity = new();
    private readonly DisplayDiagnostics diagnostics = new();
    private DisplayActivation? activation;
    private readonly ILogger<CustomButtonIntegration>? logger;
    private readonly ShowModalAction showModal;
    private readonly ArtworkColorCache artworkColors;
    public IReadOnlyList<IActionDefinition> Actions { get; }
    public IReadOnlyList<EventDefinition> EventDefinitions => ButtonEvents.Definitions;
    public CustomButtonIntegration() : this(null) { }
    public CustomButtonIntegration(ILogger<CustomButtonIntegration>? logger, IOptions<PluginHostOptions>? options = null)
    {
        this.logger = logger;
        artworkColors = new(() => context?.MusicPlayers);
        imageHostUrl = Uri.TryCreate(options?.Value.HostUrl, UriKind.Absolute, out var host) ? host : null;
        showModal = new ShowModalAction(() => context?.Deck, () => context?.Widgets, () => context?.Events);
        Actions = [new UpdateValueAction(Hub), new UpdateValuesAction(Hub), new CopyDisplayValueAction(Hub, ReadWidget), new CopyDisplayDataAction(Hub, ReadWidget), new WriteDisplayVariableAction(Hub, () => context?.UserVariables, ReadWidget), showModal];
    }
    private Task<WidgetDataSnapshot?> ReadWidget(string id, CancellationToken ct) => context?.Widgets.GetWidgetDataAsync(id, ct) ?? Task.FromResult<WidgetDataSnapshot?>(null);
    private static readonly WidgetTypeDescriptor Descriptor = new("custom-button", "Custom Button", TextCatalog.Reference("A button rendered from templates and variable data"), DefaultData: ButtonSettings.DefaultData, DataSchema: ButtonSettings.Schema, HasConfiguration: true);
    public string ProviderName => "Custom Button";
    public IReadOnlyList<WidgetTypeDescriptor> GetWidgetTypes() => [Descriptor];
    public Task InitializeAsync(IIntegrationContext context)
    {
        if (!ReferenceEquals(this.context?.Events, context.Events))
        { activation?.Dispose(); activation = new DisplayActivation(context.Events); }
        this.context = context; return Task.CompletedTask;
    }
    public async Task InitializeAsync(IWidgetTypeProviderContext context, CancellationToken cancellationToken = default)
        => await context.RegisterWidgetTypeAsync(Descriptor, cancellationToken);
    public Task ShutdownAsync() { context = null; activation?.Dispose(); activation = null; continuity.Stop(); continuity = new(); return Task.CompletedTask; }
    public IReadOnlyList<UiSurfaceDeclaration> Surfaces { get; } =
    [
        new() { Kind = UiSurfaceKinds.Widget, SessionMode = UiSessionModes.Shared },
        new() { Kind = UiSurfaceKinds.Preview, SessionMode = UiSessionModes.Shared },
        new() { Kind = UiSurfaceKinds.Config, SessionMode = UiSessionModes.Exclusive },
        new() { Kind = UiSurfaceKinds.Dialog, SessionMode = UiSessionModes.Exclusive }
    ];
    public Task<IUiSession?> CreateSessionAsync(UiSessionRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var surface = request.Surface;
        var attributes = surface.Attributes;
        if (surface.Kind == UiSurfaceKinds.Dialog)
        {
            if (!attributes.TryGetValue(UiDialogSurfaceAttributes.ViewId, out var viewId)
                || viewId.ValueKind != JsonValueKind.String || viewId.GetString() != ShowModalAction.ViewId)
                return Task.FromResult<IUiSession?>(null);
            var modalData = attributes.GetValueOrDefault(UiDialogSurfaceAttributes.Data);
            var eventWidgetId = modalData.ValueKind == JsonValueKind.Object
                && modalData.TryGetProperty("eventWidgetId", out var eventTarget) && eventTarget.ValueKind == JsonValueKind.String
                ? eventTarget.GetString() : null;
            var modalSettings = ButtonSettings.Read(modalData)
                with { WholeButtonInteraction = false };
            var completion = modalData.ValueKind == JsonValueKind.Object && modalData.TryGetProperty("closeWaitId", out var waitId) && waitId.ValueKind == JsonValueKind.String
                ? showModal.Completion(waitId.GetString()!) : null;
            var modalSession = new ButtonSession(surface, modalSettings, Hub,
                publishImageColors: true,
                modalAnswer: completion == null ? null : completion.Register,
                onOpened: context?.Events is { } openedEvents && !string.IsNullOrWhiteSpace(eventWidgetId)
                    ? () => openedEvents.Publish(ButtonEvents.ModalOpened, new Dictionary<string, object?>
                    { ["widgetId"] = eventWidgetId, ["channel"] = modalSettings.Channel }) : null,
                onControlInput: context?.Events is { } modalEvents && !string.IsNullOrWhiteSpace(eventWidgetId)
                    ? input => PublishControlEvent(modalEvents, eventWidgetId, input) : null,
                images: context != null ? new SessionImages(context.UiResources, ImageHttp,
                    message => logger?.LogWarning("{ImageDiagnostic}", message), imageHostUrl, readArtworkColor: artworkColors.ReadAsync) : null);
            if (completion != null) completion.Opened = true;
            return Task.FromResult<IUiSession?>(modalSession);
        }
        var widgetId = attributes.TryGetValue("widgetId", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null;
        if (!attributes.TryGetValue("widgetType", out var type) || type.ValueKind != JsonValueKind.String || type.GetString() != TypeId)
            return Task.FromResult<IUiSession?>(null);
        if (surface.Kind == UiSurfaceKinds.Config)
        {
            if (!attributes.TryGetValue("entryPoint", out var entry) || entry.ValueKind != JsonValueKind.String || entry.GetString() != "widget-config") return Task.FromResult<IUiSession?>(null);
            return Task.FromResult<IUiSession?>(new ConfigurationSession(surface, ButtonSettings.Read(attributes.GetValueOrDefault("widgetData")), diagnostics, Hub));
        }
        if (surface.Kind is not (UiSurfaceKinds.Widget or UiSurfaceKinds.Preview)) return Task.FromResult<IUiSession?>(null);
        var sample = attributes.TryGetValue("sample", out var sampleValue) && sampleValue.ValueKind == JsonValueKind.True;
        var settings = sample ? ButtonSettings.Default : ButtonSettings.Read(attributes.GetValueOrDefault("data"));
        var canPress = surface.Kind == UiSurfaceKinds.Widget && !(attributes.TryGetValue("ghost", out var ghost) && ghost.ValueKind == JsonValueKind.True);
        var ownerWidgetId = widgetId ?? (attributes.TryGetValue("variableScopeWidgetId", out var scope) && scope.ValueKind == JsonValueKind.String ? scope.GetString() : null);
        if (settings.Channel.Length == 0 && !string.IsNullOrWhiteSpace(ownerWidgetId))
            settings = settings with { Channel = ownerWidgetId };
        var canUpdate = !sample && (canPress || surface.Kind == UiSurfaceKinds.Preview);
        var events = context?.Events;
        void Publish(string eventId) => events?.Publish(eventId, new Dictionary<string, object?> { ["widgetId"] = ownerWidgetId });
        void PublishControl(ControlInput input) => PublishControlEvent(events!, ownerWidgetId!, input);
        var diagnosticSession = Guid.NewGuid().ToString("N");
        return Task.FromResult<IUiSession?>(new ButtonSession(surface, settings, sample ? null : Hub,
            publishImageColors: canPress && !sample,
            reportError: canUpdate && ownerWidgetId != null ? error => diagnostics.Report(diagnosticSession, ownerWidgetId, settings.Channel, surface.Kind, error) : null,
            continuity: continuity,
            images: canUpdate && context != null ? new SessionImages(context.UiResources, ImageHttp, message => logger?.LogWarning("{ImageDiagnostic}", message), imageHostUrl, readArtworkColor: artworkColors.ReadAsync) : null,
            activation: canPress && !sample && !string.IsNullOrWhiteSpace(ownerWidgetId) ? activation?.Acquire(ownerWidgetId) : null,
            diagnostic: message => logger?.LogInformation("{DisplayDiagnostic}", message),
            onWidgetEvent: canPress && !sample && ownerWidgetId != null && events != null ? Publish : null,
            onControlInput: canPress && !sample && ownerWidgetId != null && events != null ? PublishControl : null,
            sampleHistory: canUpdate,
            startDisplayTicks: canUpdate && ownerWidgetId != null && events != null
                ? () => updates.Acquire(new(ownerWidgetId, "$event:display-tick"), ct =>
                {
                    Publish(ButtonEvents.Tick); return Task.FromResult(ActionResult.Success());
                }) : null));
    }
    private static void PublishControlEvent(IEventPublisher events, string ownerWidgetId, ControlInput input)
    {
        var payload = new Dictionary<string, object?> { ["widgetId"] = ownerWidgetId, ["elementId"] = input.ElementId };
        if (input.TextValue is { } text) { payload["value"] = text; payload["previousValue"] = input.PreviousTextValue ?? ""; payload["key"] = input.DataKey; }
        if (input.Value is { } value) { payload["value"] = value; payload["previousValue"] = input.PreviousValue; payload["level"] = input.Level; payload["key"] = input.DataKey; }
        if (input.Position is { } position)
        {
            payload["x"] = position.X; payload["y"] = position.Y;
            payload["levelX"] = position.LevelX; payload["levelY"] = position.LevelY;
            payload["startX"] = position.StartX; payload["startY"] = position.StartY;
            payload["previousX"] = position.PreviousX; payload["previousY"] = position.PreviousY;
            payload["keyX"] = position.KeyX; payload["keyY"] = position.KeyY;
        }
        events.Publish(ButtonEvents.ControlEventId(input.EventName), payload);
    }
}

