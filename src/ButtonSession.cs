using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Diagnostics;
using System.Xml;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Ui;
using MacroDeck.Ui.Components;
using MacroDeck.Ui.Dsl;
using MacroDeck.Ui.Model.Events;
using MacroDeck.Ui.Model.Nodes;
using MacroDeck.Ui.Model.Patches;
using MacroDeck.Ui.Model.Surfaces;
using MacroDeck.Ui.Runtime;

namespace Ziopuzzle.CustomButton;

public sealed class ButtonSession : IUiSession
{
    private readonly object gate = new();
    private readonly UiSurface surface;
    private readonly ButtonSettings settings;
    private readonly DataHub? hub;
    private readonly SessionImages? images;
    private long lastImageRevision;
    private long lastInputRevision;
    private readonly Func<CancellationToken, Task<ActionResult>>? press;
    private readonly CancellationTokenSource lifetime = new();
    private readonly Task pump;
    private readonly Task dataPump;
    private readonly Channel<bool> refreshRequests = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    { SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.DropWrite, AllowSynchronousContinuations = false });
    private Dictionary<string, (string Type, long Since)> nodeLifetimes = new(StringComparer.Ordinal);
    private readonly Task updatePump;
    private readonly SharedScriptUpdates.Lease? sharedUpdates;
    private readonly SharedScriptUpdates.Lease? displayTicks;
    private readonly Action<string>? onWidgetEvent;
    private readonly Action<ControlInput>? onControlInput;
    private UiView? currentView;
    private UiNode? pendingRoot;
    private readonly bool sampleHistory;
    private Task pressTask = Task.CompletedTask;
    private UiTree tree;
    private UiPatch? pending;
    private string lastRoot;
    private long lastDataRevision;
    private string? lastPressError;
    private string? lastUpdateError;
    private readonly Func<string?>? previewSelection;
    private string? lastSelection;
    private string? pressError;
    private string? updateError;
    private bool disposed;
    private LayoutRenderer? renderer;
    private readonly DisplayAnimation animation;
    private readonly AnimationContinuity? continuity;
    private readonly string? continuityKey;
    private readonly DisplayActivation.Lease? activation;
    private readonly Action<string>? diagnostic;
    private readonly string diagnosticId = Guid.NewGuid().ToString("N")[..8];
    private long lastAnimationFrame;
    private string? lastRenderFailure;
    private Dictionary<string, JsonElement> initial = new();
    private string? configurationError;

    public ButtonSession(UiSurface surface, ButtonSettings settings, DataHub? hub, Func<CancellationToken, Task<ActionResult>>? press = null,
        Func<CancellationToken, Task<ActionResult>>? update = null, Func<string?>? previewSelection = null,
        Func<SharedScriptUpdates.Lease>? startSharedUpdates = null,
        Action<string>? onWidgetEvent = null, bool sampleHistory = false,
        Func<SharedScriptUpdates.Lease>? startDisplayTicks = null, Action<ControlInput>? onControlInput = null, Action<string>? diagnostic = null,
        AnimationContinuity? continuity = null, DisplayActivation.Lease? activation = null, SessionImages? images = null)
    {
        this.diagnostic = diagnostic; this.images = images;
        this.activation = activation;
        this.previewSelection = previewSelection;
        this.onWidgetEvent = onWidgetEvent; this.sampleHistory = sampleHistory;
        this.onControlInput = onControlInput;
        lastSelection = previewSelection?.Invoke();
        this.surface = surface; this.settings = settings; this.hub = hub; this.press = press;
        this.continuity = continuity;
        continuityKey = continuity == null ? null : AnimationContinuity.Key(surface, settings);
        Dictionary<string, LayoutRenderer.PadContact>? contacts = null;
        var restored = continuityKey == null ? null : continuity!.Take(continuityKey, out contacts);
        animation = restored ?? new DisplayAnimation();
        try
        {
            DataHub.ValidateName(settings.Channel);
            if (settings.Design == null || settings.Design.Preset == "xml")
            {
                renderer = new(settings.Layout);
                renderer.RestoreContacts(contacts);
                initial = DataHub.ParseValues(settings.InitialValues);
            }
        }
        catch (Exception e) when (e is FormatException or XmlException or JsonException) { configurationError = e.Message; }
        var snapshot = hub?.Snapshot(settings.Channel) ?? DataSnapshot.Empty;
        (tree, lastRoot) = BuildCurrent(snapshot, lastSelection);
        TrackNodes(tree.Root, tree.Revision);
        lastDataRevision = snapshot.Revision;
        sharedUpdates = configurationError == null ? startSharedUpdates?.Invoke() : null;
        displayTicks = configurationError == null ? startDisplayTicks?.Invoke() : null;
        if (hub != null) hub.Updated += RequestRefresh;
        dataPump = RefreshDataAsync();
        // Cover updates between the initial snapshot and subscription.
        RequestRefresh(settings.Channel);
        pump = PumpAsync();
        updatePump = update == null || configurationError != null ? Task.CompletedTask : Task.Run(() => UpdateAsync(update));
        Trace(restored == null ? "opened" : "opened; animation restored after session replacement");
    }
    public event EventHandler? Changed;
    public event EventHandler<UiSessionFaultedEventArgs>? Faulted;
    private void Trace(string message) => diagnostic?.Invoke($"Custom Button view {diagnosticId} ({settings.Channel}, {surface.Kind}): {message}");
    public UiTree BuildTree()
    {
        lock (gate)
        {
            // A snapshot becomes the transport baseline. Do not subsequently emit a patch
            // based on the older tree that this snapshot has already superseded.
            Trace($"snapshot at revision {tree.Revision}; pending base {pending?.FromRevision.ToString() ?? "none"}");
            pending = null;
            pendingRoot = null;
            if (!disposed && configurationError == null && lastRenderFailure == null) activation?.Ready();
            return tree;
        }
    }
    public IReadOnlyList<UiPatch> DrainPatches()
    {
        lock (gate) { if (pending == null) return []; var result = pending; pending = null; pendingRoot = null; return [result]; }
    }
    private (UiTree Tree, string Json) BuildCurrent(DataSnapshot snapshot, string? selectedId)
    {
        (UiTree Tree, string Json) Build(UiElement content, bool hasRootBackground = false)
        {
            // Stack supports gestures without the mandatory opaque face of a native button.
            // Keep this root stable so background changes do not reset interaction identity.
            currentView = new UiView(surface, new UiStack { Key = "root", Fill = true, Padding = 0, Gap = 0,
                Background = hasRootBackground ? default : UiValue.Of("#1a1a1a"), Children = [content],
                Events = !settings.WholeButtonInteraction ? [] : onWidgetEvent != null
                    ? new[] { UiComponentEvents.Press, UiComponentEvents.LongPress, UiComponentEvents.PressStart, UiComponentEvents.PressEnd }
                        .Select(name => UiEventHandler.On(name, () => { })).ToArray()
                    : press == null ? [] : [UiEventHandler.On(UiComponentEvents.Press, () => { })] });
            return (currentView.Tree, LayoutLimits.Validate(currentView.Tree.Root));
        }
        try
        {
            if (configurationError != null) throw new FormatException(configurationError);
            if (pressError != null) throw new FormatException(pressError);
            if (updateError != null) throw new FormatException(updateError);
            var values = new Dictionary<string, JsonElement>(initial, StringComparer.Ordinal);
            foreach (var pair in snapshot.Values) values[pair.Key] = pair.Value;
            images?.BeginRender();
            var imageRatios = new Dictionary<string, double>();
            MacroDeck.Ui.Model.Resources.UiResource? ResolveImage(string id, string source)
            {
                var image = images!.ResolveImage(id, source);
                imageRatios[id] = image.AspectRatio;
                return image.Resource;
            }
            var content = renderer != null ? renderer.Render(values, snapshot.ReadHistory, selectedId, onControlInput != null ? HandleControlInput : null,
                animation, images == null ? null : ResolveImage, id => imageRatios.GetValueOrDefault(id, 1)) : settings.Design!.Render(values);
            var result = Build(content, renderer?.HasRootBackground(values) ?? true);
            lastRenderFailure = null;
            return result;
        }
        catch (Exception e) when (e is FormatException or ArgumentException or UiViewException or JsonException)
        {
            if (lastRenderFailure != e.Message) Trace("render error; animation reset: " + e.Message);
            lastRenderFailure = e.Message;
            animation.Reset();
            return Build(new UiStack { Key = "error", Fill = true, Padding = .06, Background = "#401f28", Children =
            [ new UiTextRun { Key = "errorTitle", Text = "Custom Button", Size = .12, Color = "#ffb8c3" },
              new UiTextRun { Key = "message", Text = TextCatalog.Reference(e.Message), Size = .12, MinSize = .06, Wrap = true, MaxLines = 8, Color = "#ffb8c3" } ] });
        }
    }
    private void RequestRefresh(string channel)
    {
        if (channel == settings.Channel) refreshRequests.Writer.TryWrite(true);
    }
    private async Task RefreshDataAsync()
    {
        try
        {
            while (await refreshRequests.Reader.WaitToReadAsync(lifetime.Token))
            {
                refreshRequests.Reader.TryRead(out _);
                Refresh();
                // Bound continuous traffic without delaying the first update after an idle period.
                // The mailbox retains only a wake-up; Refresh always reads the newest snapshot.
                await Task.Delay(TimeSpan.FromMilliseconds(16), lifetime.Token);
                lock (gate) { if (!disposed && animation.IsActive) RequestRefresh(settings.Channel); }
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception e) { Faulted?.Invoke(this, new UiSessionFaultedEventArgs("Display refresh failed.", e)); }
    }
    private async Task PumpAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        try
        {
            // Maintenance keeps history, error messages and editor selection current without data writes.
            while (await timer.WaitForNextTickAsync(lifetime.Token)) RequestRefresh(settings.Channel);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception e) { Faulted?.Invoke(this, new UiSessionFaultedEventArgs("Display refresh failed.", e)); }
    }
    private async Task UpdateAsync(Func<CancellationToken, Task<ActionResult>> update)
    {
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                string? error = null;
                try
                {
                    var result = await update(lifetime.Token);
                    if (result.Status != ActionResultStatus.Succeeded) error = "Display update script failed: " + result.ErrorCode;
                }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { return; }
                catch (Exception) { error = "Could not run the display update script."; }
                lock (gate) updateError = error;
                Refresh();
                // Wait after completion so slow scripts never overlap within a session.
                await Task.Delay(TimeSpan.FromSeconds(1), lifetime.Token);
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    }
    // Explicit refresh is also used after native input, bypassing the background frame delay.
    public void Refresh()
    {
        lock (gate)
        {
            if (disposed) return;
            if (sampleHistory && configurationError == null) hub?.Sample(settings.Channel, initial);
            if (sharedUpdates != null) updateError = sharedUpdates.Error;
            var selection = previewSelection?.Invoke();
            if ((renderer?.InputRevision ?? 0) == lastInputRevision && images?.NeedsRefresh != true && (images?.Revision ?? 0) == lastImageRevision && !animation.IsActive && (hub?.Revision(settings.Channel) ?? 0) == lastDataRevision && pressError == lastPressError && updateError == lastUpdateError && selection == lastSelection) return;
            var now = Stopwatch.GetTimestamp();
            if (animation.IsActive && lastAnimationFrame != 0 && Stopwatch.GetElapsedTime(lastAnimationFrame, now).TotalMilliseconds > 250)
                Trace($"animation frame gap {Stopwatch.GetElapsedTime(lastAnimationFrame, now).TotalMilliseconds:F0} ms");
            lastAnimationFrame = now;
            var snapshot = hub?.Snapshot(settings.Channel) ?? DataSnapshot.Empty;
            lastImageRevision = images?.Revision ?? 0;
            var (next, serialized) = BuildCurrent(snapshot, selection);
            lastSelection = selection;
            lastInputRevision = renderer?.InputRevision ?? 0;
            lastDataRevision = snapshot.Revision; lastPressError = pressError; lastUpdateError = updateError;
            if (serialized == lastRoot) return;
            var previous = pending?.FromRevision ?? tree.Revision;
            pendingRoot ??= tree.Root;
            tree = next with { Revision = checked(tree.Revision + 1) };
            TrackNodes(tree.Root, tree.Revision);
            var operations = TreeChanges.Between(pendingRoot, tree.Root);
            // A burst can return to the original appearance. The revision still advanced,
            // so publish a harmless property update instead of an invalid empty patch.
            if (operations.Count == 0)
                operations = [new() { Op = UiPatchOperations.SetProperties, NodeId = tree.Root.Id, Properties = tree.Root.Properties }];
            pending = new UiPatch { FromRevision = previous, ToRevision = tree.Revision, Operations = operations };
            if (JsonSerializer.SerializeToUtf8Bytes(pending).Length > LayoutLimits.PatchBytes)
                pending = pending with { Operations = [new() { Op = UiPatchOperations.ReplaceNode, NodeId = pendingRoot.Id, Node = tree.Root }] };
            lastRoot = serialized;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }
    private void HandleControlInput(ControlInput input)
    {
        if (input.Position is { } position)
            hub?.Update(settings.Channel, new Dictionary<string, JsonElement>
            {
                [position.KeyX] = JsonSerializer.SerializeToElement(position.X),
                [position.KeyY] = JsonSerializer.SerializeToElement(position.Y)
            });
        if (input.DataKey != null && input.Value is { } value)
            hub?.Update(settings.Channel, new Dictionary<string, JsonElement> { [input.DataKey] = input.BooleanValue is { } on ? JsonSerializer.SerializeToElement(on) : JsonSerializer.SerializeToElement(value) });
        onControlInput?.Invoke(input);
    }
    private void TrackNodes(UiNode root, long revision)
    {
        var next = new Dictionary<string, (string Type, long Since)>(StringComparer.Ordinal);
        void Visit(UiNode node)
        {
            next[node.Id] = nodeLifetimes.TryGetValue(node.Id, out var old) && old.Type == node.Type
                ? old : (node.Type, revision);
            foreach (var child in node.Children) Visit(child);
        }
        Visit(root);
        nodeLifetimes = next;
    }
    public void Dispatch(UiEvent uiEvent)
    {
        DispatchCore(uiEvent);
        Refresh();
    }
    private void DispatchCore(UiEvent uiEvent)
    {
        lock (gate)
        {
            if (disposed) return;
            // A data patch can overtake a user's input in transit. Accept older revisions only
            // for nodes continuously present since that revision; removed/recreated nodes stay stale.
            if (uiEvent.Revision is { } revision && (revision > tree.Revision
                || !nodeLifetimes.TryGetValue(uiEvent.NodeId, out var node) || revision < node.Since)) return;
            if (uiEvent.NodeId != "root")
            {
                // The current SDK view validates node identity and declared event names, including
                // generated alpha wrappers. Our outer session owns the transport revision.
                currentView?.Dispatch(uiEvent with { Revision = null });
                return;
            }
            if (!settings.WholeButtonInteraction) return;
            var eventId = uiEvent.Name switch
            {
                UiComponentEvents.Press => "short-press", UiComponentEvents.LongPress => "long-press",
                UiComponentEvents.PressStart => "touch-start", UiComponentEvents.PressEnd => "touch-end", _ => null
            };
            if (eventId != null) onWidgetEvent?.Invoke(eventId);
            if (press == null || uiEvent.Name != UiComponentEvents.Press || !pressTask.IsCompleted) return;
            pressTask = Task.Run(async () =>
            {
                string? error = null;
                try
                {
                    var result = await press(lifetime.Token);
                    if (result.Status != ActionResultStatus.Succeeded) error = "Script execution failed: " + result.ErrorCode;
                }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { return; }
                catch (Exception) { error = "Could not run the script. Check the Macro Deck log."; }
                lock (gate) pressError = error;
            });
        }
    }
    public async ValueTask DisposeAsync()
    {
        Task running;
        lock (gate)
        {
            if (disposed) return;
            disposed = true; running = pressTask;
            if (continuityKey != null && configurationError == null && lastRenderFailure == null)
                continuity!.Save(continuityKey, animation, renderer);
        }
        if (hub != null) hub.Updated -= RequestRefresh;
        activation?.Dispose();
        refreshRequests.Writer.TryComplete();
        await lifetime.CancelAsync();
        if (sharedUpdates != null) await sharedUpdates.DisposeAsync();
        if (displayTicks != null) await displayTicks.DisposeAsync();
        await Task.WhenAll(pump, dataPump, running, updatePump);
        if (images != null) await images.DisposeAsync();
        Trace("closed");
        lifetime.Dispose();
    }
}
