using System.Text.Json;
using MacroDeck.Sdk.Events;

namespace Ziopuzzle.CustomButton;

/// <summary>One activation per visible widget, gated by a snapshot and known event binding.</summary>
public sealed class DisplayActivation : IDisposable
{
    private readonly object gate = new();
    private readonly IEventPublisher events;
    private readonly TimeProvider clock;
    private readonly Dictionary<string, Entry> entries = [];
    private bool disposed;
    internal sealed class Entry
    {
        public int Users, Ready;
        public bool Published;
        public long Closed;
    }
    public DisplayActivation(IEventPublisher events, TimeProvider? clock = null)
    { this.events = events; this.clock = clock ?? TimeProvider.System; events.BindingsChanged += Check; }
    public Lease Acquire(string widgetId)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var now = clock.GetTimestamp();
            foreach (var key in entries.Where(p => p.Value.Users == 0 && clock.GetElapsedTime(p.Value.Closed, now) >= TimeSpan.FromSeconds(2)).Select(p => p.Key).ToArray()) entries.Remove(key);
            if (!entries.TryGetValue(widgetId, out var entry)) entries[widgetId] = entry = new();
            entry.Users++;
            return new Lease(this, entry);
        }
    }
    private bool IsBound(string widgetId) => events.GetBindings().Any(binding =>
    {
        if (binding.EventId != ButtonEvents.Activated) return false;
        if (!binding.Parameters.TryGetValue("widgetId", out var target) || target.Value is not { } value || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return true;
        // Dynamic references and non-equality filters are evaluated by the host, never locally.
        return target.Operator != "==" || value.ValueKind != JsonValueKind.String || value.GetString() == widgetId;
    });
    private void Check()
    {
        lock (gate)
        {
            if (disposed) return;
            foreach (var pair in entries)
            {
                var entry = pair.Value;
                if (entry.Ready == 0 || entry.Published || !IsBound(pair.Key)) continue;
                entry.Published = true;
                events.Publish(ButtonEvents.Activated, new Dictionary<string, object?> { ["widgetId"] = pair.Key });
            }
        }
    }
    public sealed class Lease : IDisposable
    {
        private readonly DisplayActivation owner;
        private readonly Entry entry;
        private bool ready, closed;
        internal Lease(DisplayActivation owner, Entry entry)
        { this.owner = owner; this.entry = entry; }
        public void Ready()
        {
            lock (owner.gate)
            {
                if (closed || ready || owner.disposed) return;
                ready = true; entry.Ready++;
                owner.Check();
            }
        }
        public void Dispose()
        {
            lock (owner.gate)
            {
                if (closed) return;
                closed = true;
                if (ready) entry.Ready--;
                if (--entry.Users == 0) entry.Closed = owner.clock.GetTimestamp();
                // Only idle entries are evictable; active viewers retain their activation state.
                while (owner.entries.Count(p => p.Value.Users == 0) > 128)
                    owner.entries.Remove(owner.entries.Where(p => p.Value.Users == 0).MinBy(p => p.Value.Closed).Key);
            }
        }
    }
    public void Dispose()
    {
        lock (gate) { disposed = true; entries.Clear(); }
        events.BindingsChanged -= Check;
    }
}
