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
    private bool echoFlows;
    private int revisionOffset;
    private readonly Func<JsonElement> currentFlows;
    private readonly bool publishInitialCorrection;
    private Timer? initialCorrectionTimer;
    private int initialCorrectionStarted, initialCorrectionReady;
    public ConfigurationSession(UiSurface surface, ButtonSettings settings, DisplayDiagnostics? diagnostics = null, DataHub? hub = null)
    {
        var channel = new UiState<string>(settings.Channel);
        var layout = new UiState<string>(settings.Layout);
        var values = new UiState<string>(settings.InitialValues);
        var wholeButtonInteraction = new UiState<bool>(settings.WholeButtonInteraction);
        var widgetId = surface.Attributes.TryGetValue("widgetId", out var owner) && owner.ValueKind == JsonValueKind.String ? owner.GetString() : null;
        if (!string.IsNullOrWhiteSpace(widgetId) && (settings.Channel.Length == 0
            || settings.Channel == settings.ConfigurationWidgetId)) channel.Value = widgetId;
        var draftHasError = new UiState<bool>(false);
        var diagnosticErrors = new UiState<IReadOnlyList<string>>([]);
        string ReadDiagnostics()
        {
            var report = $"Custom Button diagnostics\nWidget: {widgetId ?? "unsaved"}\nChannel: {channel.Value}\nChecked: {DateTimeOffset.UtcNow:O}\n\n";
            draftHasError.Value = false;
            string? draftError = null;
            try
            {
                DataHub.ValidateName(channel.Value);
                var data = DataHub.ParseValues(values.Value);
                var snapshot = hub?.Snapshot(channel.Value) ?? DataSnapshot.Empty;
                foreach (var pair in snapshot.Values) data[pair.Key] = pair.Value;
                _ = new LayoutRenderer(layout.Value).Render(data, snapshot.ReadHistory);
                report += "Current draft: XML and data rendering check passed. Image downloads and host rendering are not checked here.";
            }
            catch (Exception e) when (e is FormatException or System.Xml.XmlException or JsonException or ArgumentException)
            { draftHasError.Value = true; draftError = e.Message; report += "Current draft: rendering check failed."; }
            var snapshotReport = (diagnostics ?? new DisplayDiagnostics()).ReadSnapshot(widgetId, draftError);
            diagnosticErrors.Value = snapshotReport.Errors;
            var observed = snapshotReport.Report;
            return report + "\n\n" + (string.IsNullOrEmpty(observed) ? "No runtime rendering errors recorded for this widget in this plugin process." : observed)
                + "\n\nClosed-session errors are retained as last-observed reports. Action execution logs remain available in Macro Deck.";
        }
        var errorReport = new UiState<string>(ReadDiagnostics());
        publishInitialCorrection = !string.IsNullOrWhiteSpace(widgetId) && (settings.ConfigurationWidgetId != widgetId || settings.Channel != channel.Value);
        var flows = new UiState<JsonElement>(settings.Flows);
        currentFlows = () => flows.Value;
        var orderTarget = new UiState<string>("");
        var flowMessage = new UiState<string>("");
        string? copiedWidgetMessage = null;
        // Repair existing self-target filters when reopening, as well as on editor changes.
        try
        {
            flows.Value = ButtonEvents.Normalize(flows.Value, widgetId);
            flows.Value = ActionEditorDefaults.ChangeChannel(flows.Value, settings.Channel, channel.Value);
            if (!string.IsNullOrWhiteSpace(widgetId) && !string.IsNullOrWhiteSpace(settings.ConfigurationWidgetId)
                && settings.ConfigurationWidgetId != widgetId)
            {
                flows.Value = ActionEditorDefaults.RetargetEvents(flows.Value, widgetId, settings.ConfigurationWidgetId);
                copiedWidgetMessage = "Copied/imported widget detected. Event targets matching the source widget have been updated. Save to apply.";
                if (channel.Value != settings.Channel)
                    copiedWidgetMessage = "Copied/imported widget detected. Event targets, the automatic channel and matching display-data actions have been updated. Save to apply.";
            }
        }
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
                    new UiStringInput { Key = "channel", Label = TextCatalog.Reference("Channel"), Description = "Changing this also updates matching channels in this widget's display-data actions. Buttons on the same channel share data.", LiteralOnly = true,
                        Binding = Bind.Custom(() => channel.Value, value =>
                        {
                            flows.Value = ActionEditorDefaults.ChangeChannel(flows.Value, channel.Value, value);
                            channel.Value = value;
                        }) },
                    // Persist provenance through the normal configuration transaction, not a runtime write.
                    new UiStringInput { Key = "configurationWidgetId", LiteralOnly = true,
                        Binding = Bind.Custom(() => widgetId ?? settings.ConfigurationWidgetId, _ => { }),
                        VisibleWhen = new UiVisibleWhen { ParameterName = "channel", Values = [], SiblingValue = () => channel.Value } },
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
                        ..(copiedWidgetMessage == null ? Array.Empty<UiElement>() : new UiElement[]
                        {
                            new UiBanner { Key = "copiedWidgetNotice", Text = copiedWidgetMessage, Severity = "error" }
                        }),
                        new UiConfigButton { Key = "retargetEvents", Label = "Target this widget in all events", Events = [UiEventHandler.On(UiConfigEvents.Activate, () =>
                        {
                            try
                            {
                                flows.Value = ActionEditorDefaults.RetargetEvents(flows.Value, widgetId);
                                flowMessage.Value = "All event widget targets now refer to this widget. Save to apply.";
                            }
                            catch (FormatException e) { flowMessage.Value = e.Message; }
                        })] },
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
                                try
                                {
                                    var normalized = ActionEditorDefaults.ApplyChannel(flows.Value, ButtonEvents.Normalize(value, widgetId), channel.Value);
                                    // The editor updates optimistically. Even if our canonical value
                                    // is unchanged, it must receive the corrected value again.
                                    echoFlows |= normalized.GetRawText() != value.GetRawText();
                                    flows.Value = normalized; flowMessage.Value = "";
                                }
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
                    new UiCodeInput { Key = "layout", Label = TextCatalog.Reference("Drawing XML"), HideLabel = true, Language = "xml", LiteralOnly = true, MaxLength = LayoutLimits.XmlCharacters, Binding = Bind.Custom(() => layout.Value, SetXml) },
                        new UiProse { Key = "nativeStatusText", Text = UiText.Optional(() => TextCatalog.Reference(editorStatus.Value)) },
                        new UiProse { Key = "historyHelp", Text = TextCatalog.Reference("While displayed, the current value is sampled every second. To fetch new variable values, add a data update action to the display update event under Actions.") }
                    ] },
                    new UiTab { Key = "diagnosticsTab", Label = "Diagnostics", Children =
                    [
                        new UiProse { Key = "diagnosticsHelp", Text = "Refresh after reproducing an error or editing the layout. Use the copy button below to copy the report from this settings screen." },
                        new UiConfigButton { Key = "refreshDiagnostics", Label = "Refresh diagnostics", Events = [UiEventHandler.On(UiConfigEvents.Activate, () => errorReport.Value = ReadDiagnostics())] },
                        new UiProse { Key = "diagnosticsStatus",
                            Severity = UiValue.From(() => draftHasError.Value ? "error" : "success"),
                            Text = UiText.Optional(() => draftHasError.Value ? "Current draft: rendering check failed." : "Current draft: XML and data rendering check passed.") },
                        new UiRepeat<string> {
                            Key = "diagnosticErrors", Items = UiValue.From(() => diagnosticErrors.Value),
                            KeySelector = message => "error" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(message))),
                            Template = (message, key) => new UiProse { Key = key, Text = message }
                        },
                        new UiCopyValue { Key = "errorReport", Label = "Diagnostic report", Value = UiValue.From(() => errorReport.Value) }
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
    public UiTree BuildTree()
    {
        var tree = view.Tree;
        // The host intentionally ignores values in the opening snapshot for dirty tracking.
        // Send a later revision so both remapped filters and ownership metadata enter its draft.
        if (publishInitialCorrection && !disposed && Interlocked.Exchange(ref initialCorrectionStarted, 1) == 0)
            initialCorrectionTimer = new Timer(_ =>
            {
                if (disposed) return;
                Interlocked.Exchange(ref initialCorrectionReady, 1);
                Changed?.Invoke(this, EventArgs.Empty);
            }, null, TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
        return tree with { Revision = checked(tree.Revision + revisionOffset) };
    }
    public IReadOnlyList<UiPatch> DrainPatches()
    {
        var initialCorrection = Interlocked.Exchange(ref initialCorrectionReady, 0) != 0;
        if (initialCorrection) echoFlows = true;
        var patches = view.DrainPatches().Select(p => p with {
            FromRevision = checked(p.FromRevision + revisionOffset), ToRevision = checked(p.ToRevision + revisionOffset) }).ToList();
        if (!echoFlows) return patches;
        echoFlows = false;
        var operation = new UiPatchOperation { Op = UiPatchOperations.SetProperties, NodeId = "flows",
            Properties = new Dictionary<string, JsonElement> { ["value"] = currentFlows() } };
        var operations = new List<UiPatchOperation> { operation };
        if (initialCorrection)
        {
            foreach (var field in view.Tree.Root.Children.SelectMany(n => n.Children)
                .Where(n => n.Id is "channel" or "configurationWidgetId"))
                operations.Add(new UiPatchOperation { Op = UiPatchOperations.SetProperties, NodeId = field.Id,
                    Properties = new Dictionary<string, JsonElement> { ["value"] = field.Properties["value"] } });
        }
        if (patches.Count > 0)
            patches[^1] = patches[^1] with { Operations = patches[^1].Operations.Concat(operations).ToArray() };
        else
        {
            var from = checked(view.Tree.Revision + revisionOffset);
            revisionOffset = checked(revisionOffset + 1);
            patches.Add(new UiPatch { FromRevision = from, ToRevision = checked(from + 1), Operations = operations });
        }
        return patches;
    }
    public void Dispatch(UiEvent uiEvent)
    {
        if (disposed) return;
        view.Dispatch(uiEvent with { Revision = uiEvent.Revision is { } revision ? revision - revisionOffset : null });
        if (echoFlows) Changed?.Invoke(this, EventArgs.Empty);
    }
    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        if (initialCorrectionTimer != null) await initialCorrectionTimer.DisposeAsync();
        view.Changed -= OnChanged;
        view.HandlerFaulted -= OnHandlerFaulted;
        await nativeEditor.DisposeAsync();
    }
}




