namespace Ziopuzzle.CustomButton;

/// <summary>Bounded in-process reports, isolated by widget and rendering session.</summary>
public sealed class DisplayDiagnostics
{
    private readonly object gate = new();
    private readonly Dictionary<string, Entry> entries = new();
    private sealed record Entry(string Widget, string Channel, string Surface, DateTimeOffset Time, string Error);
    public void Report(string session, string widget, string channel, string surface, string? error)
    {
        lock (gate)
        {
            if (error == null) { entries.Remove(session); return; }
            if (entries.TryGetValue(session, out var prior) && prior.Error == error) return;
            if (entries.Count >= 128 && !entries.ContainsKey(session)) entries.Remove(entries.MinBy(e => e.Value.Time).Key);
            entries[session] = new(widget, channel, surface, DateTimeOffset.UtcNow, error.Length > 8000 ? error[..8000] + "…" : error);
        }
    }
    public string Read(string? widget, string? draftError = null)
        => ReadSnapshot(widget, draftError).Report;

    public (string Report, IReadOnlyList<string> Errors) ReadSnapshot(string? widget, string? draftError = null)
    {
        lock (gate)
        {
            var groups = entries.Values.Where(e => widget != null && e.Widget == widget)
                .GroupBy(e => e.Error).OrderByDescending(group => group.Max(e => e.Time)).ToArray();
            var errors = (draftError == null ? Enumerable.Empty<string>() : new[] { draftError })
                .Concat(groups.Select(group => group.Key)).Distinct(StringComparer.Ordinal).Take(4).ToArray();
            var report = string.Join("\n\n", errors.Select(error =>
            {
                var sources = groups.FirstOrDefault(group => group.Key == error);
                var details = sources == null ? "" : string.Join("\n", sources.GroupBy(e => (e.Channel, e.Surface))
                    .Select(group => $"Last observed {group.Key.Surface}: {group.Max(e => e.Time):O}; Channel: {group.Key.Channel}; {group.Count()} session(s)."));
                return error + (error == draftError ? "\nDetected in current draft." : "")
                    + (details.Length == 0 ? "" : "\n" + details);
            }));
            return (report, errors);
        }
    }
}
