using MacroDeck.Sdk.Ui;
using MacroDeck.Ui.Config;
using MacroDeck.Ui.Config.Options;
using MacroDeck.Ui.Dsl;
using MacroDeck.Ui.Model.Events;
using MacroDeck.Ui.Model.Nodes;
using MacroDeck.Ui.Model.Patches;
using MacroDeck.Ui.Model.Surfaces;
using MacroDeck.Ui.Runtime;
using System.Text.Json;

namespace Ziopuzzle.CustomButton;

public sealed class ConfigurationSession : IUiSession
{
    private readonly UiView view;
    private readonly NativeEditorSession nativeEditor = new();
    private bool disposed;
    public ConfigurationSession(UiSurface surface, ButtonSettings settings)
    {
        var channel = new UiState<string>(settings.Channel);
        var layout = new UiState<string>(settings.Layout);
        var values = new UiState<string>(settings.InitialValues);
        var wholeButtonInteraction = new UiState<bool>(settings.WholeButtonInteraction);
        var widgetId = surface.Attributes.TryGetValue("widgetId", out var owner) && owner.ValueKind == JsonValueKind.String ? owner.GetString() : null;
        var flows = new UiState<JsonElement>(settings.Flows);
        var orderTarget = new UiState<string>("");
        var flowMessage = new UiState<string>("");
        // Repair existing self-target filters when reopening, as well as on editor changes.
        try { flows.Value = ButtonEvents.Normalize(flows.Value, widgetId); }
        catch (FormatException e) { flowMessage.Value = e.Message; }
        var mode = new UiState<string>(settings.Design?.Preset ?? "xml");
        var editorStatus = new UiState<string>("Build the layout in a separate editor window. The preview remains on this screen.");
        var inputTarget = new UiState<string>("");
        void AddEvent(Func<JsonElement> create)
        {
            try
            {
                var next = create();
                flowMessage.Value = next.GetArrayLength() == flows.Value.GetArrayLength() ? "An event with these conditions already exists." : "Event added. Configure its actions and save.";
                flows.Value = next;
            }
            catch (FormatException e) { flowMessage.Value = e.Message; }
        }
        void SetXml(string xml)
        {
            layout.Value = xml; mode.Value = "xml";
            if (inputTarget.Value != "$other" && !ButtonEvents.InputTargets(xml).Any(t => t.Id == inputTarget.Value)) inputTarget.Value = "";
        }
        void OpenEditor()
        {
            try { _ = new LayoutDocument(layout.Value); nativeEditor.Open(layout.Value, values.Value, () => layout.Value, SetXml, text => editorStatus.Value = text); }
            catch (Exception e) when (e is FormatException or System.Xml.XmlException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
            { editorStatus.Value = "Cannot open editor: " + e.Message; }
        }
        view = new(surface, new UiWidgetConfiguration
        {
            Key = "root",
            Properties = new UiWidgetProperties
            {
                Key = "properties", Children =
                [
                    new UiStringInput { Key = "channel", Label = TextCatalog.Reference("Channel"), Description = TextCatalog.Reference("Use the same channel as the update action. Buttons on the same channel share data."), LiteralOnly = true, Binding = Bind.To(channel) },
                    new UiBooleanInput { Key = "wholeButtonInteraction", Label = "Whole button interaction", Description = "Disable whole-button gestures and their press effect. Interactive elements and sliders remain available.", Binding = Bind.To(wholeButtonInteraction) },
                    new UiJsonInput { Key = "initialValues", Label = TextCatalog.Reference("Initial data JSON (before receiving data)"), LiteralOnly = true, Binding = Bind.To(values) },
                    new UiProse { Key = "hint", Text = TextCatalog.Reference("Use matching data names in your update actions and display layout.") }
                ]
            },
            Editor = new UiWidgetEditor
            {
                Key = "editor", Children =
                [
                    new UiTabs { Key = "configurationTabs", Children =
                    [
                    new UiTab { Key = "actionsTab", Label = TextCatalog.Reference("Actions"), Children =
                    [
                        new UiProse { Key = "flowHelp", Text = TextCatalog.Reference("Configure actions for each event and save before testing. Check event targets after duplicating a button.") },
                        new UiProse { Key = "controlHelp", Text = TextCatalog.Reference("Select a target, then choose + Add event. Display activated, Display update and Variable changed are under Other (updates and variables). Use Display activated to read current values when the saved button becomes visible. Select the watched variable after adding Variable changed.") },
                        new UiConfigStack { Key = "eventShortcuts", Direction = "horizontal", Wrap = true, Children =
                        [
                            new UiChoiceInput { Key = "inputTarget", Label = TextCatalog.Reference("Input target"), HideLabel = true, Transient = true, LiteralOnly = true,
                                Options = UiValue.From<IReadOnlyList<UiOption>>(() => new[] { new UiOption { Value = "", Label = TextCatalog.Reference("Whole button") } }
                                    .Concat(ButtonEvents.InputTargets(layout.Value).Select(t => new UiOption { Value = t.Id, Label = t.Id + " (" + t.Kind + ")" }))
                                    .Append(new UiOption { Value = "$other", Label = TextCatalog.Reference("Other (updates and variables)") }).ToArray()),
                                Binding = Bind.To(inputTarget) },
                            new UiChoiceInput { Key = "addInputEvent", Label = TextCatalog.Reference("Add event"), HideLabel = true, Transient = true, LiteralOnly = true,
                                Options = UiValue.From<IReadOnlyList<UiOption>>(() => new[] { new UiOption { Value = "", Label = TextCatalog.Reference("+ Add event") } }
                                    .Concat(inputTarget.Value == "$other"
                                        ? new[] { new UiOption { Value = ButtonEvents.Activated, Label = TextCatalog.Reference("Display activated") }, new UiOption { Value = ButtonEvents.Tick, Label = TextCatalog.Reference("Display update (1 second)") }, new UiOption { Value = "variable-changed", Label = TextCatalog.Reference("Variable changed") } }
                                        : ButtonEvents.InputEvents(layout.Value, inputTarget.Value).Select(d => new UiOption { Value = d.Id, Label = d.Name })).ToArray()),
                                Binding = Bind.Custom(() => "", id =>
                                {
                                    if (id.Length == 0) return;
                                    if (inputTarget.Value == "$other")
                                    {
                                        if (id == ButtonEvents.Tick) AddEvent(() => ButtonEvents.AddDisplayTick(flows.Value, widgetId));
                                        else if (id == ButtonEvents.Activated) AddEvent(() => ButtonEvents.AddElementEvent(flows.Value, widgetId, id, null, ""));
                                        else if (id == "variable-changed") AddEvent(() => ButtonEvents.AddVariableChanged(flows.Value, widgetId));
                                    }
                                    else AddEvent(() => ButtonEvents.AddElementEvent(flows.Value, widgetId, id, inputTarget.Value, layout.Value));
                                }) }
                        ] },
                        new UiProse { Key = "flowMessage", Text = UiText.Optional(() => TextCatalog.Reference(flowMessage.Value)) },
                        new UiProse { Key = "elementTargets", Text = UiText.Optional(() => ButtonEvents.ElementTargetHelp(flows.Value, layout.Value, widgetId)) },
                        new UiConfigStack { Key = "eventOrder", Direction = "vertical", Children =
                        [
                            new UiChoiceInput { Key = "eventOrderTarget", Label = "Event order", Transient = true, LiteralOnly = true,
                                Options = UiValue.From<IReadOnlyList<UiOption>>(() => new[] { new UiOption { Value = "", Label = "Choose an event to reorder" } }
                                    .Concat(flows.Value.EnumerateArray().Select((flow, index) => new UiOption {
                                        Value = flow.TryGetProperty("triggerId", out var id) ? id.GetString() ?? "" : "",
                                        Label = ButtonEvents.FlowLabel(flow, index) })).ToArray()), Binding = Bind.To(orderTarget) },
                            new UiConfigStack { Key = "eventOrderButtons", Direction = "horizontal", Wrap = true, Children =
                            [
                                new UiConfigButton { Key = "moveEventUp", Label = "Move up", Events = [UiEventHandler.On(UiConfigEvents.Activate, () => flows.Value = ButtonEvents.MoveFlow(flows.Value, orderTarget.Value, -1))] },
                                new UiConfigButton { Key = "moveEventDown", Label = "Move down", Events = [UiEventHandler.On(UiConfigEvents.Activate, () => flows.Value = ButtonEvents.MoveFlow(flows.Value, orderTarget.Value, 1))] }
                            ] }
                        ] },
                        new UiActionsListEditor { Key = "flows", CanRun = true, LiteralOnly = true,
                            Binding = Bind.Custom(() => flows.Value, value =>
                            {
                                try { flows.Value = ButtonEvents.Normalize(value, widgetId); flowMessage.Value = ""; }
                                catch (Exception e) when (e is FormatException or JsonException or InvalidOperationException) { flowMessage.Value = e.Message; }
                            }) }
                    ] },
                    new UiTab { Key = "drawingTab", Label = TextCatalog.Reference("Drawing"), Children =
                    [
                    // Keep the legacy discriminator in submitted drafts without a visible selector.
                    new UiStringInput { Key = "designPreset", LiteralOnly = true, Binding = Bind.To(mode),
                        VisibleWhen = new UiVisibleWhen { ParameterName = "channel", Values = [], SiblingValue = () => channel.Value } },
                    new UiConfigStack { Key = "templateToolbar", Direction = "vertical", Children =
                    [
                        new UiProse { Key = "layoutHeading", Text = TextCatalog.Reference("Drawing XML") },
                        new UiConfigButton { Key = "openNativeEditor", Label = "Open editor",
                            Events = [UiEventHandler.On(UiConfigEvents.Activate, OpenEditor)] },
                        new UiChoiceInput { Key = "templatePicker", Label = "Templates", HideLabel = true, Transient = true, LiteralOnly = true,
                            Options = UiValue.Of<IReadOnlyList<UiOption>>(new[] { new UiOption { Value = "", Label = "Choose a template" } }
                                .Concat(LayoutTemplates.All.Select(t => new UiOption { Value = t.Id, Label = TextCatalog.Reference(t.Name), Badge = t.Category })).ToArray()),
                            Binding = Bind.Custom(() => "", id => { if (LayoutTemplates.All.Any(t => t.Id == id)) { var selected = LayoutTemplates.Get(id); SetXml(selected.Xml); values.Value = selected.InitialValues; } }) }
                    ] },
                    new UiCodeInput { Key = "layout", Label = TextCatalog.Reference("Drawing XML"), HideLabel = true, Language = "xml", LiteralOnly = true, MaxLength = 16000, Binding = Bind.Custom(() => layout.Value, SetXml) },
                        new UiProse { Key = "nativeStatusText", Text = UiText.Optional(() => TextCatalog.Reference(editorStatus.Value)) },
                        new UiProse { Key = "historyHelp", Text = TextCatalog.Reference("While displayed, the current value is sampled every second. To fetch new variable values, add a data update action to the display update event under Actions.") }
                    ] }

                    ] }
                ]
            }
        });
        view.Changed += OnChanged;
        view.HandlerFaulted += OnHandlerFaulted;
    }
    private void OnChanged(object? sender, EventArgs e) => Changed?.Invoke(this, e);
    private void OnHandlerFaulted(object? sender, UiHandlerFaultEventArgs e)
        => Faulted?.Invoke(this, new UiSessionFaultedEventArgs("Configuration interaction failed.", e.Exception));
    public event EventHandler? Changed;
    public event EventHandler<UiSessionFaultedEventArgs>? Faulted;
    public UiTree BuildTree() => view.Tree;
    public IReadOnlyList<UiPatch> DrainPatches() => view.DrainPatches();
    public void Dispatch(UiEvent uiEvent) { if (!disposed) view.Dispatch(uiEvent); }
    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        view.Changed -= OnChanged;
        view.HandlerFaulted -= OnHandlerFaulted;
        await nativeEditor.DisposeAsync();
    }
}




