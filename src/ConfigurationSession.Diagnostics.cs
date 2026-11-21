using System.Text.Json;
using MacroDeck.Ui.Runtime;

namespace Ziopuzzle.CustomButton;

public sealed partial class ConfigurationSession
{
    private readonly DisplayDiagnostics? diagnostics;
    private readonly DataHub? hub;
    private readonly UiState<bool> draftHasError = new(false);
    private readonly UiState<IReadOnlyList<string>> diagnosticErrors = new([]);
    private readonly UiState<IReadOnlyList<KeyValuePair<string, string>>> dataRows = new([]);
    private readonly UiState<string> errorReport;
    private sealed record CheckedDraft(string Channel, string Layout, string Values);
    private readonly UiState<CheckedDraft?> checkedDraft = new(null);

    private bool DiagnosticsAreCurrent() => checkedDraft.Value is { } draft
        && channel.Value == draft.Channel && layout.Value == draft.Layout && values.Value == draft.Values;

    private string ReadDiagnostics()
    {
        checkedDraft.Value = new(channel.Value, layout.Value, values.Value);
        var report = $"Custom Button diagnostics\nWidget: {widgetId ?? "unsaved"}\nChannel: {channel.Value}\nChecked: {DateTimeOffset.UtcNow:O}\n\n";
        draftHasError.Value = false;
        dataRows.Value = [];
        string? draftError = null;
        try
        {
            DataHub.ValidateName(channel.Value);
            var data = DataHub.ParseValues(values.Value);
            var snapshot = hub?.Snapshot(channel.Value) ?? DataSnapshot.Empty;
            foreach (var pair in snapshot.Values) data[pair.Key] = pair.Value;
            dataRows.Value = data.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p =>
            {
                var raw = p.Value.GetRawText();
                var origin = snapshot.Values.ContainsKey(p.Key) ? "live" : "initial";
                return new KeyValuePair<string, string>(p.Key, $"{p.Key} [{DisplayData.TypeOf(p.Value)}, {origin}] = " + (raw.Length > 500 ? raw[..500] + "…" : raw));
            }).ToArray();
            new LayoutRenderer(layout.Value).ValidateDraft(data, history: snapshot.ReadHistory);
            report += "Current draft: XML and data rendering check passed. Image downloads and host rendering are not checked here.";
        }
        catch (Exception e) when (e is FormatException or System.Xml.XmlException or JsonException or ArgumentException)
        { draftHasError.Value = true; draftError = e.Message; report += "Current draft: rendering check failed."; }
        var snapshotReport = (diagnostics ?? new DisplayDiagnostics()).ReadSnapshot(widgetId, draftError);
        diagnosticErrors.Value = snapshotReport.Errors;
        var observed = snapshotReport.Report;
        return report + "\n\n" + (string.IsNullOrEmpty(observed) ? "No runtime rendering errors recorded for this widget in this plugin process." : observed)
            + "\n\nOlder preview errors are cleared when this widget's preview renders successfully. Other closed-session errors remain as last-observed reports. Action execution logs remain available in Macro Deck.";
    }
}
