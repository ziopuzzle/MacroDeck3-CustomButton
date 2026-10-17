using MacroDeck.Sdk.MusicPlayer;

namespace Ziopuzzle.CustomButton;

/// <summary>Coalesces demand-driven colour reads across visible sessions; never downloads images.</summary>
public sealed class ArtworkColorCache(Func<IMusicPlayerApi?> players, TimeProvider? clock = null)
{
    private sealed record Entry(Task<string?> Work, DateTimeOffset Expires);
    private readonly object gate = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly TimeProvider time = clock ?? TimeProvider.System;
    public Task<string?> ReadAsync(string player, CancellationToken cancellationToken)
    {
        Task<string?> work;
        lock (gate)
        {
            var now = time.GetUtcNow();
            if (!entries.TryGetValue(player, out var entry) || entry.Work.IsCompleted && now >= entry.Expires)
            {
                // Avoid retaining every player ever seen across integration reconfiguration.
                foreach (var key in entries.Where(p => p.Value.Work.IsCompleted && now >= p.Value.Expires).Select(p => p.Key).ToArray())
                    entries.Remove(key);
                if (entries.Count >= 256) return Task.FromResult<string?>(null);
                entry = new(Task.Run(() => ReadCoreAsync(player)), now.AddSeconds(1));
                entries[player] = entry;
            }
            work = entry.Work;
        }
        // A closing preview must not cancel a read shared by a live widget.
        return work.WaitAsync(cancellationToken);
    }
    private async Task<string?> ReadCoreAsync(string player)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var api = players();
        if (api == null) return null;
        var color = await api.GetAlbumColorAsync(player, timeout.Token).WaitAsync(timeout.Token);
        return color == null ? null : DisplayColor.Parse(color).Hex;
    }
}
