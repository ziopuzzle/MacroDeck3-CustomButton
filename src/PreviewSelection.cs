using System.Collections.Concurrent;
using MacroDeck.Ui.Components;
using MacroDeck.Ui.Dsl;

namespace Ziopuzzle.CustomButton;

// Ephemeral editor selection, shared only with the same widget's preview, never live deck buttons.
public sealed class PreviewSelection
{
    private sealed class Entry { public string? Selected; }
    private readonly ConcurrentDictionary<string, Entry> entries = new();
    public (Action<string?> Select, Action Close) Open(string widgetId)
    {
        var entry = new Entry(); entries[widgetId] = entry;
        return (id => Volatile.Write(ref entry.Selected, id), () => ((ICollection<KeyValuePair<string, Entry>>)entries).Remove(new(widgetId, entry)));
    }
    public string? Read(string widgetId) => entries.TryGetValue(widgetId, out var entry) ? Volatile.Read(ref entry.Selected) : null;
    public static UiElement Highlight(UiElement element) => element switch
    {
        UiStack stack => stack with { Background = "#665522" },
        UiButton button => button with { BorderStyle = "static", BorderColor = "#ffd54f" },
        UiLayer layer => layer with { Children = layer.Children.Select(Highlight).ToArray() },
        UiTextRun text => text with { Color = "#ffd54f" },
        UiRangeBar bar => bar with { StartColor = "#ffd54f", EndColor = "#ffd54f" },
        UiChart chart => chart with { Color = "#ffd54f" },
        UiClockDial clock => clock with { Color = "#ffd54f" },
        UiProgressBar progress => progress with { StartColor = "#ffd54f", EndColor = "#ffd54f" },
        UiSlider slider => slider with { LevelColor = "#ffd54f" },
        _ => element
    };
}
