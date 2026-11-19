using MacroDeck.Sdk.Ui;
using MacroDeck.Ui.Model.Events;
using MacroDeck.Ui.Model.Nodes;
using MacroDeck.Ui.Model.Patches;
using MacroDeck.Ui.Model.Surfaces;
using MacroDeck.Ui.Runtime;
using System.Text.Json;

namespace Ziopuzzle.CustomButton;

public sealed partial class ConfigurationSession : IUiSession
{
    private readonly UiView view;
    private readonly NativeEditorSession nativeEditor = new();
    private bool disposed;
    private bool echoFlows;
    private int revisionOffset;
    private readonly bool publishInitialCorrection;
    private Timer? initialCorrectionTimer;
    private int initialCorrectionStarted, initialCorrectionReady;
    public ConfigurationSession(UiSurface surface, ButtonSettings settings, DisplayDiagnostics? diagnostics = null, DataHub? hub = null)
    {
        this.diagnostics = diagnostics;
        this.hub = hub;
        channel = new(settings.Channel);
        layout = new(settings.Layout);
        values = new(settings.InitialValues);
        wholeButtonInteraction = new(settings.WholeButtonInteraction);
        widgetId = surface.Attributes.TryGetValue("widgetId", out var owner) && owner.ValueKind == JsonValueKind.String ? owner.GetString() : null;
        configurationWidgetId = widgetId ?? settings.ConfigurationWidgetId;
        if (!string.IsNullOrWhiteSpace(widgetId) && (settings.Channel.Length == 0
            || settings.Channel == settings.ConfigurationWidgetId)) channel.Value = widgetId;
        errorReport = new(ReadDiagnostics());
        flows = new(settings.Flows);
        publishInitialCorrection = InitializeFlows(settings);
        mode = new(settings.Design?.Preset ?? "xml");
        try { lastValidLayout = new LayoutDocument(layout.Value); }
        catch (Exception e) when (e is FormatException or System.Xml.XmlException or ArgumentException) { }
        view = new(surface, BuildConfiguration());
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
            Properties = new Dictionary<string, JsonElement> { ["value"] = flows.Value } };
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
