using System.Text.Json;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Ui;
using MacroDeck.Ui.Config;
using MacroDeck.Ui.Config.Options;
using MacroDeck.Ui.Dsl;
using MacroDeck.Ui.Model.Events;
using MacroDeck.Ui.Model.Nodes;
using MacroDeck.Ui.Model.Patches;
using MacroDeck.Ui.Runtime;

namespace Ziopuzzle.CustomButton;

// Action configuration works independently of the owning widget's configuration provider.
public sealed class ModalConfigurationSession : IUiSession
{
    private readonly UiView view;
    private readonly ILayoutEditorSession editor;
    private bool disposed;

    public ModalConfigurationSession(ActionConfigurationRequest request, ILayoutEditorSession? editor = null,
        Func<MacroDeck.Sdk.Widgets.IWidgetApi?>? widgets = null)
    {
        this.editor = editor ?? new NativeEditorSession();
        UiState<string> State(string key, string fallback = "") => new(
            request.Parameters.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()! : fallback);
        var title = State("title", "Custom Button modal");
        var device = State("deviceId");
        var channel = State("channel", "demo");
        var target = State("eventWidgetId");
        var layout = State("layout", ShowModalAction.SampleLayout);
        var initial = State("initialValues", "{}");
        var edited = new UiState<string?>(null);
        var saveReminder = new UiState<bool>(false);
        var status = new UiState<string>("");
        IReadOnlyList<UiOption> Targets() => new[] { new UiOption { Value = "$self", Label = "This widget" } }
            .Concat((widgets?.Invoke()?.GetWidgets() ?? []).Select(w => new UiOption { Value = w.Id, Label = w.Label + " — " + w.Location }))
            .ToArray();
        var targets = new UiState<IReadOnlyList<UiOption>>(Targets());
        void OpenEditor()
        {
            try
            {
                _ = new LayoutDocument(layout.Value);
                _ = DataHub.ParseValues(initial.Value);
                this.editor.Open(edited.Value ?? layout.Value, initial.Value, () => edited.Value ?? layout.Value,
                    xml => { if (!disposed) { edited.Value = xml == layout.Value ? null : xml; status.Value = "Click Apply edited XML below, then save in Macro Deck."; } },
                    message => { if (!disposed) status.Value = edited.Value != null
                        ? "Edited XML is waiting. Click Apply edited XML below, then save in Macro Deck." : message; });
            }
            catch (Exception e) when (e is FormatException or System.Xml.XmlException or JsonException or IOException
                or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
            { status.Value = "Cannot open editor: " + e.Message; }
        }
        view = new UiView(request.Session.Surface, new UiStep
        {
            Key = "modalConfiguration", StepId = "modalConfiguration", Title = "Show modal",
            Children =
            [
                new UiStringInput { Key = "title", Label = "Modal title", Binding = Bind.To(title) },
                new UiDevicePickerInput { Key = "deviceId", Label = "Destination device",
                    Description = "Optional for a standard button press. Required for Custom Button events. An offline device fails without falling back to another device.", Binding = Bind.To(device) },
                new UiStringInput { Key = "channel", Label = "Channel", Required = true, Binding = Bind.To(channel) },
                new UiAutocompleteInput { Key = "eventWidgetId", Label = "Event target widget",
                    Options = UiValue.From(() => targets.Value), AllowsCustomValue = true,
                    Events = [UiEventHandler.On(UiConfigEvents.Open, () => targets.Value = Targets())],
                    Description = "Defaults to the button running this action. Used for modal input and Modal opened events.", Binding = Bind.To(target) },
                new UiCodeInput { Key = "initialValues", Label = "Initial data JSON", Language = "json", Required = true, LiteralOnly = true,
                    Description = "Local fallback values only. Use {} to disable; existing Channel values take priority.", Binding = Bind.To(initial) },
                new UiConfigButton { Key = "openModalEditor", Label = "Open editor", Events = [UiEventHandler.On(UiConfigEvents.Activate, OpenEditor)] },
                new UiProse { Key = "editorHelp", Text = "After GUI editing, click Apply edited XML here and save in Macro Deck. Reopen the modal to see layout changes. There is no modal preview." },
                new UiWhen { Key = "saveReminderVisible", Condition = () => saveReminder.Value, Content = () =>
                    new UiProse { Key = "saveReminder", Severity = "warning",
                        Text = "Remember to save in Macro Deck. Applying XML updates the action draft; it does not save the widget." } },
                new UiWhen { Key = "xmlMode", Condition = () => edited.Value == null, Content = () =>
                    new UiCodeInput { Key = "layout", Label = "Drawing XML", Language = "xml", Required = true, LiteralOnly = true,
                        MaxLength = LayoutLimits.XmlCharacters, Binding = Bind.To(layout) } },
                // The host action card only stores client change events, not server value patches.
                // Keep the actual parameter ID on the acceptance input so it writes the XML itself.
                new UiWhen { Key = "applyMode", Condition = () => edited.Value != null, Content = () => new UiConfigStack { Key = "pendingEditor", Children =
                [
                    new UiProse { Key = "pendingWarning", Severity = "warning", Text = "Unapplied editor changes will be lost when this action is collapsed or its settings are closed. Apply them below, then save in Macro Deck." },
                    new UiChoiceInput { Key = "layout", Label = "Editor changes", HideLabel = true, LiteralOnly = true, Segmented = true,
                        Options = UiValue.From<IReadOnlyList<UiOption>>(() => new[] {
                            new UiOption { Value = edited.Value!, Label = "Apply edited XML" },
                            new UiOption { Value = layout.Value, Label = "Discard editor changes" } }),
                        Binding = Bind.Custom(() => "", xml => {
                            if (xml.Length == 0 || (xml != edited.Value && xml != layout.Value)) return;
                            var apply = xml != layout.Value;
                            layout.Value = xml; edited.Value = null;
                            if (apply) saveReminder.Value = true;
                            status.Value = apply ? "XML applied. Save in Macro Deck, then reopen the modal."
                                : "Editor changes discarded. Reopen the editor if it reports a conflict.";
                        }) },
                    new UiCopyValue { Key = "editedXml", Label = "Edited XML (recovery copy)", Value = UiValue.From(() => edited.Value ?? "") }
                ] } },
                new UiWhen { Key = "editorStatusVisible", Condition = () => !string.IsNullOrWhiteSpace(status.Value), Content = () =>
                    new UiProse { Key = "editorStatus", Text = UiValue.From(() => status.Value) } }
            ]
        });
        view.Changed += OnChanged;
        view.HandlerFaulted += OnFaulted;
    }
    private void OnChanged(object? sender, EventArgs e) => Changed?.Invoke(this, e);
    private void OnFaulted(object? sender, UiHandlerFaultEventArgs e)
        => Faulted?.Invoke(this, new UiSessionFaultedEventArgs("Modal configuration interaction failed.", e.Exception));
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
        view.HandlerFaulted -= OnFaulted;
        await editor.DisposeAsync();
    }
}
