using System.Text.Json;
using MacroDeck.Sdk.Actions;
using MacroDeck.Ui.Components;
using MacroDeck.Ui.Model.Events;
using MacroDeck.Ui.Model.Nodes;

namespace Ziopuzzle.CustomButton;

public sealed partial class ButtonSession
{
    private Dictionary<string, (string Type, long Since)> nodeLifetimes = new(StringComparer.Ordinal);
    private Task pressTask = Task.CompletedTask;

    private void HandleControlInput(ControlInput input)
    {
        if (input.DataKey != null && input.TextValue is { } text)
        {
            var previous = hub?.UpdateValue(settings.Channel, input.DataKey, JsonSerializer.SerializeToElement(text));
            if (previous is { } raw) input = input with { PreviousTextValue = raw.ToString() };
        }
        if (input.Position is { } position)
            hub?.Update(settings.Channel, new Dictionary<string, JsonElement>
            {
                [position.KeyX] = JsonSerializer.SerializeToElement(position.X),
                [position.KeyY] = JsonSerializer.SerializeToElement(position.Y)
            });
        if (input.DataKey != null && input.Value is { } value)
        {
            var previous = hub?.UpdateValue(settings.Channel, input.DataKey, input.BooleanValue is { } on ? JsonSerializer.SerializeToElement(on) : JsonSerializer.SerializeToElement(value));
            if (previous is { } raw)
            {
                if (input.BooleanValue != null && (raw.ToString() == "1" || bool.TryParse(raw.ToString(), out _)))
                    input = input with { PreviousValue = raw.ToString() == "1" || bool.Parse(raw.ToString()) ? 1 : 0 };
                else if (double.TryParse(raw.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number) && double.IsFinite(number))
                    input = input with { PreviousValue = number };
            }
        }
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
}
