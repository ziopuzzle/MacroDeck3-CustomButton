using System.Text.Json;
using MacroDeck.Ui.Runtime;

namespace Ziopuzzle.CustomButton;

public sealed partial class ConfigurationSession
{
    private readonly UiState<string> channel, layout, values, mode;
    private readonly UiState<bool> wholeButtonInteraction;
    private readonly UiState<JsonElement> flows;
    private readonly UiState<string> orderTarget = new("");
    private readonly UiState<string> flowMessage = new("");
    private readonly UiState<string> editorStatus = new("Build the layout in a separate editor window. The preview remains on this screen.");
    private readonly UiState<string> inputTarget = new("");
    private readonly string? widgetId;
    private readonly string configurationWidgetId;
    private string? copiedWidgetMessage;
    private LayoutDocument? lastValidLayout;
    private sealed record TemplateRecovery(string Layout, string Values, string Mode, string AppliedLayout, string AppliedValues);
    private readonly UiState<TemplateRecovery?> templateRecovery = new(null);

    private bool InitializeFlows(ButtonSettings settings)
    {
        var migratedTargets = false;
        // Normalize action flows, retaining the host's self-target sentinel.
        try
        {
            flows.Value = ButtonEvents.Normalize(flows.Value, widgetId);
            flows.Value = ActionEditorDefaults.ChangeChannel(flows.Value, settings.Channel, channel.Value);
            if (!string.IsNullOrWhiteSpace(widgetId) && !string.IsNullOrWhiteSpace(settings.ConfigurationWidgetId)
                && settings.ConfigurationWidgetId != widgetId)
            {
                var beforeRetarget = flows.Value.GetRawText();
                flows.Value = ActionEditorDefaults.RetargetEvents(flows.Value, widgetId, settings.ConfigurationWidgetId);
                var targetsChanged = flows.Value.GetRawText() != beforeRetarget;
                if (targetsChanged && channel.Value != settings.Channel)
                    copiedWidgetMessage = "Copied/imported widget detected. Event targets, the automatic channel and matching display-data actions have been updated. Save to apply.";
                else if (targetsChanged)
                    copiedWidgetMessage = "Copied/imported widget detected. Event targets matching the source widget have been updated. Save to apply.";
                else if (channel.Value != settings.Channel)
                    copiedWidgetMessage = "Copied/imported widget detected. The automatic channel and matching display-data actions have been updated. Save to apply.";
            }
            (flows.Value, migratedTargets) = ButtonEvents.MigrateOwnTargets(flows.Value, widgetId);
            if (migratedTargets && copiedWidgetMessage == null)
                copiedWidgetMessage = "Event targets referring to this widget have been updated to This widget. Save to apply.";
        }
        catch (FormatException e) { flowMessage.Value = e.Message; }
        return !string.IsNullOrWhiteSpace(widgetId) && (settings.ConfigurationWidgetId != widgetId || settings.Channel != channel.Value || migratedTargets);
    }

    private void AddEvent(Func<JsonElement> create)
    {
        try
        {
            var next = create();
            flowMessage.Value = next.GetArrayLength() == flows.Value.GetArrayLength() ? "An event with these conditions already exists." : "Event added. Configure its actions and save.";
            flows.Value = next;
        }
        catch (FormatException e) { flowMessage.Value = e.Message; }
    }
    private void SetXml(string xml)
    {
        if (templateRecovery.Value is { } recovery && xml != recovery.AppliedLayout) templateRecovery.Value = null;
        try
        {
            var next = new LayoutDocument(xml);
            if (lastValidLayout != null)
            {
                var renames = ElementEventReferences.Renames(lastValidLayout, next);
                flows.Value = ElementEventReferences.Retarget(flows.Value, widgetId, renames);
                if (renames.TryGetValue(inputTarget.Value, out var renamedTarget)) inputTarget.Value = renamedTarget;
            }
            lastValidLayout = next;
        }
        catch (Exception e) when (e is FormatException or System.Xml.XmlException or ArgumentException) { }
        layout.Value = xml; mode.Value = "xml";
        if (inputTarget.Value != "$other" && !ButtonEvents.InputTargets(xml).Any(t => t.Id == inputTarget.Value)) inputTarget.Value = "";
    }
    private void SetInitialValues(string json)
    {
        if (templateRecovery.Value is { } recovery && json != recovery.AppliedValues) templateRecovery.Value = null;
        values.Value = json;
    }
    private void OpenEditor()
    {
        try { _ = new LayoutDocument(layout.Value); nativeEditor.Open(layout.Value, values.Value, () => layout.Value, SetXml,
            text => editorStatus.Value = text, hub?.Snapshot(channel.Value).Values); }
        catch (Exception e) when (e is FormatException or System.Xml.XmlException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        { editorStatus.Value = "Cannot open editor: " + e.Message; }
    }

    private void ChangeChannel(string value)
    {
        flows.Value = ActionEditorDefaults.ChangeChannel(flows.Value, channel.Value, value);
        channel.Value = value;
    }

    private void RetargetEvents()
    {
        try
        {
            flows.Value = ActionEditorDefaults.RetargetEvents(flows.Value, widgetId);
            flowMessage.Value = "All event widget targets now refer to this widget. Save to apply.";
        }
        catch (FormatException e) { flowMessage.Value = e.Message; }
    }

    private void AddInputEvent(string id)
    {
        if (id.Length == 0) return;
        if (inputTarget.Value == "$other")
        {
            if (id == ButtonEvents.Tick) AddEvent(() => ButtonEvents.AddDisplayTick(flows.Value, widgetId));
            else if (id is ButtonEvents.Activated or ButtonEvents.ModalOpened or ButtonEvents.ModalClosed) AddEvent(() => ButtonEvents.AddElementEvent(flows.Value, widgetId, id, null, ""));
            else if (id == "variable-changed") AddEvent(() => ButtonEvents.AddVariableChanged(flows.Value, widgetId));
        }
        else AddEvent(() => ButtonEvents.AddElementEvent(flows.Value, widgetId, id, inputTarget.Value, layout.Value));
    }

    private void SetFlows(JsonElement value)
    {
        try
        {
            var normalized = ActionEditorDefaults.ApplyChannel(flows.Value, ButtonEvents.Normalize(value, widgetId), channel.Value, widgetId);
            // The editor updates optimistically. Even if our canonical value
            // is unchanged, it must receive the corrected value again.
            echoFlows |= normalized.GetRawText() != value.GetRawText();
            flows.Value = normalized; flowMessage.Value = "";
        }
        catch (Exception e) when (e is FormatException or JsonException or InvalidOperationException)
        {
            echoFlows = true;
            flowMessage.Value = "Action changes rejected. The last valid actions have been kept. " + e.Message;
        }
    }

    private void SelectTemplate(string id)
    {
        if (!LayoutTemplates.All.Any(t => t.Id == id)) return;
        var selected = LayoutTemplates.Get(id);
        var recovery = new TemplateRecovery(layout.Value, values.Value, mode.Value, selected.Xml, selected.InitialValues);
        lastValidLayout = null;
        SetXml(selected.Xml);
        values.Value = selected.InitialValues;
        templateRecovery.Value = recovery;
    }

    private bool CanRestoreTemplate() => templateRecovery.Value is { } recovery
        && layout.Value == recovery.AppliedLayout && values.Value == recovery.AppliedValues && mode.Value == "xml";

    private void RestoreTemplate()
    {
        if (!CanRestoreTemplate()) return;
        var recovery = templateRecovery.Value!;
        templateRecovery.Value = null;
        // Restoring a drawing must not infer ID renames in the preserved action flows.
        lastValidLayout = null;
        SetXml(recovery.Layout);
        values.Value = recovery.Values;
        mode.Value = recovery.Mode;
    }
}
