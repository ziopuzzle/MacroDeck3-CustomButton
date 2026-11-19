using MacroDeck.Ui.Config;
using MacroDeck.Ui.Config.Options;
using MacroDeck.Ui.Dsl;
using MacroDeck.Ui.Runtime;
using System.Text.Json;

namespace Ziopuzzle.CustomButton;

public sealed partial class ConfigurationSession
{
    private UiWidgetConfiguration BuildConfiguration() => new()
    {
        Key = "root",
        Properties = BuildProperties(),
        Editor = new UiWidgetEditor
        {
            Key = "editor", Children =
            [
                new UiTabs { Key = "configurationTabs", Children = [BuildActionsTab(), BuildDrawingTab(), BuildDiagnosticsTab()] }
            ]
        }
    };

    private UiWidgetProperties BuildProperties() => new UiWidgetProperties
    {
        Key = "properties", Children =
        [
            new UiStringInput { Key = "channel", Label = TextCatalog.Reference("Channel"), Description = "Changing this also updates matching channels in this widget's display-data actions. Buttons on the same channel share data.", LiteralOnly = true,
                Binding = Bind.Custom(() => channel.Value, ChangeChannel) },
            // Persist provenance through the normal configuration transaction, not a runtime write.
            new UiStringInput { Key = "configurationWidgetId", LiteralOnly = true,
                Binding = Bind.Custom(() => configurationWidgetId, _ => { }),
                VisibleWhen = new UiVisibleWhen { ParameterName = "channel", Values = [], SiblingValue = () => channel.Value } },
            new UiBooleanInput { Key = "wholeButtonInteraction", Label = "Whole button interaction", Description = "Disable whole-button gestures and their press effect. Interactive elements and sliders remain available.", Binding = Bind.To(wholeButtonInteraction) },
            new UiJsonInput { Key = "initialValues", Label = "Initial data JSON",
                Description = "Defaults for top-level data names missing from the Channel. Removing a Channel value makes its default apply again. These defaults do not initialize live Channel data.",
                LiteralOnly = true, Binding = Bind.Custom(() => values.Value, SetInitialValues) },
            new UiProse { Key = "hint", Text = TextCatalog.Reference("Use matching data names in your update actions and display layout.") }
        ]
    };

    private UiTab BuildActionsTab() => new UiTab { Key = "actionsTab", Label = TextCatalog.Reference("Actions"), Children =
    [
        ..(copiedWidgetMessage == null ? Array.Empty<UiElement>() : new UiElement[]
        {
            new UiBanner { Key = "copiedWidgetNotice", Text = copiedWidgetMessage, Severity = "error" }
        }),
        new UiConfigButton { Key = "retargetEvents", Label = "Target this widget in all events", Events = [UiEventHandler.On(UiConfigEvents.Activate, RetargetEvents)] },
        new UiProse { Key = "flowHelp", Text = TextCatalog.Reference("Configure actions for each event and save before testing. This widget targets follow copies; check explicit widget targets after duplicating.") },
        new UiProse { Key = "controlHelp", Text = TextCatalog.Reference("Select a target, then choose + Add event. Modal opened, Display activated, Display update and Variable changed are under Other (updates and variables). Use Display activated to read current values when the saved button becomes visible. Select the watched variable after adding Variable changed.") },
        new UiConfigStack { Key = "eventShortcuts", Direction = "horizontal", Wrap = true, Children =
        [
            new UiChoiceInput { Key = "inputTarget", Searchable = true, Label = TextCatalog.Reference("Input target"), HideLabel = true, Transient = true, LiteralOnly = true,
                Options = UiValue.From<IReadOnlyList<UiOption>>(() => new[] { new UiOption { Value = "", Label = TextCatalog.Reference("Whole button") } }
                    .Concat(ButtonEvents.InputTargets(layout.Value).Select(t => new UiOption { Value = t.Id, Label = t.Id + " (" + t.Kind + ")" }))
                    .Append(new UiOption { Value = "$other", Label = TextCatalog.Reference("Other (updates and variables)") }).ToArray()),
                Binding = Bind.To(inputTarget) },
            new UiChoiceInput { Key = "addInputEvent", Label = TextCatalog.Reference("Add event"), HideLabel = true, Transient = true, LiteralOnly = true,
                Options = UiValue.From<IReadOnlyList<UiOption>>(() => new[] { new UiOption { Value = "", Label = TextCatalog.Reference("+ Add event") } }
                    .Concat(inputTarget.Value == "$other"
                        ? new[] { new UiOption { Value = ButtonEvents.Activated, Label = TextCatalog.Reference("Display activated") }, new UiOption { Value = ButtonEvents.ModalOpened, Label = TextCatalog.Reference("Modal opened") }, new UiOption { Value = ButtonEvents.ModalClosed, Label = TextCatalog.Reference("Modal closed") }, new UiOption { Value = ButtonEvents.Tick, Label = TextCatalog.Reference("Display update (1 second)") }, new UiOption { Value = "variable-changed", Label = TextCatalog.Reference("Variable changed") } }
                        : ButtonEvents.InputEvents(layout.Value, inputTarget.Value).Select(d => new UiOption { Value = d.Id, Label = d.Name })).ToArray()),
                Binding = Bind.Custom(() => "", AddInputEvent) }
        ] },
        new UiProse { Key = "flowMessage", Text = UiText.Optional(() => TextCatalog.Reference(flowMessage.Value)) },
        new UiProse { Key = "elementTargets", Text = UiText.Optional(() => ButtonEvents.ElementTargetHelp(flows.Value, layout.Value, widgetId)) },
        new UiConfigStack { Key = "eventOrder", Direction = "vertical", Children =
        [
            new UiChoiceInput { Key = "eventOrderTarget", Searchable = true, Label = "Event order", Transient = true, LiteralOnly = true,
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
            Binding = Bind.Custom(() => flows.Value, SetFlows) }
    ] };

    private UiTab BuildDrawingTab() => new UiTab { Key = "drawingTab", Label = TextCatalog.Reference("Drawing"), Children =
    [
        // Keep the legacy discriminator in submitted drafts without a visible selector.
        new UiStringInput { Key = "designPreset", LiteralOnly = true, Binding = Bind.To(mode),
            VisibleWhen = new UiVisibleWhen { ParameterName = "channel", Values = [], SiblingValue = () => channel.Value } },
        new UiConfigStack { Key = "templateToolbar", Direction = "vertical", Children =
        [
            new UiProse { Key = "layoutHeading", Text = TextCatalog.Reference("Drawing XML") },
            new UiConfigButton { Key = "openNativeEditor", Label = "Open editor",
                Events = [UiEventHandler.On(UiConfigEvents.Activate, OpenEditor)] },
            new UiChoiceInput { Key = "templatePicker", Searchable = true, Label = "Templates", HideLabel = true, Transient = true, LiteralOnly = true,
                Description = "Replaces Drawing XML and Initial data JSON. Channel and actions are kept.",
                Options = UiValue.Of<IReadOnlyList<UiOption>>(new[] { new UiOption { Value = "", Label = "Choose a template" } }
                    .Concat(LayoutTemplates.All.Select(t => new UiOption { Value = t.Id, Label = $"[{t.Category}] {t.Name}" })).ToArray()),
                Binding = Bind.Custom(() => "", SelectTemplate) },
            new UiWhen { Key = "templateRecoveryVisible", Condition = CanRestoreTemplate, Content = () =>
                new UiConfigStack { Key = "templateRecovery", Children =
                [
                    new UiConfigButton { Key = "restoreTemplate", Label = "Restore previous drawing",
                        Events = [UiEventHandler.On(UiConfigEvents.Activate, RestoreTemplate)] },
                    new UiProse { Key = "templateRecoveryHelp", Text = "Restores the XML and initial JSON from before the last template selection. Available until you edit either field or close settings." }
                ] } }
        ] },
        new UiCodeInput { Key = "layout", Label = TextCatalog.Reference("Drawing XML"), HideLabel = true, Language = "xml", LiteralOnly = true, MaxLength = LayoutLimits.XmlCharacters, Binding = Bind.Custom(() => layout.Value, SetXml) },
        new UiProse { Key = "elementReferenceWarnings", Text = UiText.Optional(() => string.Join("\n",
            ButtonEvents.ElementTargetHelp(flows.Value, layout.Value, widgetId).Split('\n').Where(line => line.StartsWith("'", StringComparison.Ordinal)))) },
        new UiProse { Key = "nativeStatusText", Text = UiText.Optional(() => TextCatalog.Reference(editorStatus.Value)) },
        new UiProse { Key = "historyHelp", Text = TextCatalog.Reference("While displayed, the current value is sampled every second. To fetch new variable values, add a data update action to the display update event under Actions.") }
    ] };

    private UiTab BuildDiagnosticsTab() => new UiTab { Key = "diagnosticsTab", Label = "Diagnostics", Children =
    [
        new UiProse { Key = "diagnosticsHelp", Text = "Refresh after reproducing an error or editing the layout. Results below remain from the last check until refreshed. Use the copy button below to copy that report." },
        new UiConfigButton { Key = "refreshDiagnostics", Label = "Refresh diagnostics", Events = [UiEventHandler.On(UiConfigEvents.Activate, () => errorReport.Value = ReadDiagnostics())] },
        new UiProse { Key = "diagnosticsStatus",
            Severity = UiValue.From(() => !DiagnosticsAreCurrent() ? "warning" : draftHasError.Value ? "error" : "success"),
            Text = UiText.Optional(() => !DiagnosticsAreCurrent() ? "Draft changed since the last check. Refresh diagnostics to check the current XML and data."
                : draftHasError.Value ? "Current draft: rendering check failed." : "Current draft: XML and data rendering check passed.") },
        new UiRepeat<string> {
            Key = "diagnosticErrors", Items = UiValue.From(() => diagnosticErrors.Value),
            KeySelector = message => "error" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(message))),
            Template = (message, key) => new UiProse { Key = key, Text = message }
        },
        new UiCopyValue { Key = "errorReport", Label = "Diagnostic report", Value = UiValue.From(() => errorReport.Value) },
        new UiHeading { Key = "displayDataHeading", Text = "Display data at last check" },
        new UiProse { Key = "displayDataHelp", Text = "Snapshot at the last refresh: live channel values override initial JSON. Missing keys are not listed. Values are truncated after 500 characters." },
        new UiRepeat<KeyValuePair<string, string>> {
            Key = "displayDataRows", Items = UiValue.From(() => dataRows.Value),
            KeySelector = row => "data" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(row.Key))),
            Template = (row, key) => new UiProse { Key = key, Text = row.Value }
        }
    ] };

}
