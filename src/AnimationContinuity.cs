using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MacroDeck.Ui.Model.Surfaces;

namespace Ziopuzzle.CustomButton;

/// <summary>Short-lived, single-use checkpoints, never shared mutable animation state.</summary>
public sealed class AnimationContinuity(TimeProvider? timeProvider = null)
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly object gate = new();
    private readonly Dictionary<string, (long Saved, DisplayAnimation State)> entries = [];
    private bool stopped;
    private const int Capacity = 64;
    public static string? Key(UiSurface surface, ButtonSettings settings)
    {
        if (surface.Kind is not ("widget" or "preview")) return null;
        var attrs = surface.Attributes;
        if (new[] { "sample", "ghost" }.Any(k => attrs.TryGetValue(k, out var value) && value.ValueKind == JsonValueKind.True)) return null;
        var owner = attrs.GetValueOrDefault("widgetId");
        if (owner.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(owner.GetString()))
            owner = attrs.GetValueOrDefault("variableScopeWidgetId");
        if (owner.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(owner.GetString())) return null;
        // Include all supplied viewer/draft attributes and configuration, not just the data channel.
        var signature = JsonSerializer.Serialize(new { surface.Kind, surface.SessionMode,
            Attributes = attrs.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value), settings });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature)));
    }
    private void Prune()
    {
        var now = clock.GetTimestamp();
        foreach (var key in entries.Where(p => clock.GetElapsedTime(p.Value.Saved, now) >= TimeSpan.FromSeconds(2)).Select(p => p.Key).ToArray()) entries.Remove(key);
    }
    public DisplayAnimation? Take(string key)
    {
        lock (gate) { Prune(); return !stopped && entries.Remove(key, out var entry) ? entry.State : null; }
    }
    public void Save(string key, DisplayAnimation animation)
    {
        lock (gate)
        {
            if (stopped) return;
            Prune();
            if (!entries.ContainsKey(key) && entries.Count >= Capacity) entries.Remove(entries.MinBy(p => p.Value.Saved).Key);
            entries[key] = (clock.GetTimestamp(), animation.Copy());
        }
    }
    public void Stop() { lock (gate) { stopped = true; entries.Clear(); } }
}
