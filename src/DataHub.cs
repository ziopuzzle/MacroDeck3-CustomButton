using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Collections.Frozen;

namespace Ziopuzzle.CustomButton;

// All data is transient. The host remains the source of truth for variables.
public sealed class DataHub(TimeProvider? timeProvider = null)
{
    public const int HistoryLimit = 120;
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly Dictionary<string, Dictionary<string, List<(long Second, double Value)>>> history = new(StringComparer.Ordinal);
    private readonly object gate = new();
    private readonly Dictionary<string, Dictionary<string, JsonElement>> channels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> revisions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DataSnapshot> snapshots = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> sampledSeconds = new(StringComparer.Ordinal);
    // Subscribers only enqueue work. Notify outside the data lock so rendering never blocks writers.
    internal event Action<string>? Updated;
    public void Sample(string channel, IReadOnlyDictionary<string, JsonElement> initialValues)
    {
        lock (gate)
        {
            var second = clock.GetUtcNow().ToUnixTimeSeconds();
            if (sampledSeconds.TryGetValue(channel, out var sampled) && sampled == second) return;
            ValidateName(channel);
            if (!channels.ContainsKey(channel))
            {
                if (channels.Count >= 256) throw new FormatException("The maximum of 256 channels has been reached.");
                channels[channel] = new(StringComparer.Ordinal);
            }
            var values = new Dictionary<string, JsonElement>(initialValues, StringComparer.Ordinal);
            foreach (var pair in channels[channel]) values[pair.Key] = pair.Value;
            if (!history.TryGetValue(channel, out var series)) history[channel] = series = new(StringComparer.Ordinal);
            bool changed = false;
            foreach (var pair in values)
            {
                var raw = pair.Value.ValueKind == JsonValueKind.String ? pair.Value.GetString() : pair.Value.ToString();
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number)) continue;
                if (!series.TryGetValue(pair.Key, out var points)) series[pair.Key] = points = [];
                if (points.Count > 0 && points[^1].Second == second) continue;
                points.Add((second, number));
                if (points.Count > HistoryLimit) points.RemoveAt(0);
                changed = true;
            }
            sampledSeconds[channel] = second;
            if (changed) { revisions[channel] = revisions.GetValueOrDefault(channel) + 1; snapshots.Remove(channel); }
        }
    }
    public long Revision(string channel) { lock (gate) return revisions.GetValueOrDefault(channel); }
    public DataSnapshot Snapshot(string channel)
    {
        lock (gate)
        {
            if (snapshots.TryGetValue(channel, out var snapshot)) return snapshot;
            if (!channels.TryGetValue(channel, out var data)) return DataSnapshot.Empty;
            return snapshots[channel] = new DataSnapshot(revisions.GetValueOrDefault(channel),
                data.ToFrozenDictionary(StringComparer.Ordinal),
                history.TryGetValue(channel, out var series)
                    ? series.ToFrozenDictionary(p => p.Key, p => (IReadOnlyList<double>)Array.AsReadOnly(p.Value.Select(v => v.Value).ToArray()), StringComparer.Ordinal)
                    : FrozenDictionary<string, IReadOnlyList<double>>.Empty);
        }
    }
    public static void ValidateName(string name)
    {
        if (!Regex.IsMatch(name, @"\A[A-Za-z0-9_.-]{1,64}\z", RegexOptions.CultureInvariant))
            throw new FormatException("Names must contain 1–64 letters, digits, underscores, dots or hyphens.");
    }
    public static Dictionary<string, JsonElement> ParseValues(string json)
    {
        if (json.Length > 16000) throw new FormatException("Data must not exceed 16000 characters.");
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new FormatException("Data must be a JSON object.");
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var item in document.RootElement.EnumerateObject())
        {
            ValidateName(item.Name);
            if (item.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array) throw new FormatException("Values must be strings, numbers, booleans or null.");
            if (!result.TryAdd(item.Name, item.Value.Clone())) throw new FormatException("The data contains duplicate keys.");
        }
        if (result.Count > 64) throw new FormatException("Data must contain at most 64 entries.");
        return result;
    }
    public Dictionary<string, JsonElement> Read(string channel)
    {
        lock (gate) return channels.TryGetValue(channel, out var values) ? new(values, StringComparer.Ordinal) : new(StringComparer.Ordinal);
    }
    public IReadOnlyList<double> ReadHistory(string channel, string key, int count)
    {
        if (count < 2 || count > HistoryLimit) throw new FormatException("History length must be between 2 and 120.");
        lock (gate) return history.TryGetValue(channel, out var series) && series.TryGetValue(key, out var points)
            ? points.TakeLast(count).Select(p => p.Value).ToArray() : [];
    }
    public void Update(string channel, IReadOnlyDictionary<string, JsonElement> values, bool replace = false)
        => UpdateCore(channel, values, replace, null);
    internal JsonElement? UpdateValue(string channel, string key, JsonElement value)
        => UpdateCore(channel, new Dictionary<string, JsonElement> { [key] = value }, false, key);
    private JsonElement? UpdateCore(string channel, IReadOnlyDictionary<string, JsonElement> values, bool replace, string? previousKey)
    {
        ValidateName(channel);
        JsonElement? previous = null;
        lock (gate)
        {
            if (previousKey != null && channels.TryGetValue(channel, out var prior) && prior.TryGetValue(previousKey, out var oldValue)) previous = oldValue;
            if (!channels.ContainsKey(channel) && channels.Count >= 256) throw new FormatException("The maximum of 256 channels has been reached.");
            var merged = !replace && channels.TryGetValue(channel, out var old) ? new Dictionary<string, JsonElement>(old, StringComparer.Ordinal) : new(StringComparer.Ordinal);
            foreach (var pair in values) { ValidateName(pair.Key); merged[pair.Key] = pair.Value.Clone(); }
            // Revalidate the combined payload, so small updates cannot grow it without bound.
            ParseValues(JsonSerializer.Serialize(merged));
            channels[channel] = merged;
            if (!history.TryGetValue(channel, out var series)) history[channel] = series = new(StringComparer.Ordinal);
            if (replace) foreach (var key in series.Keys.Except(merged.Keys).ToArray()) series.Remove(key);
            var second = clock.GetUtcNow().ToUnixTimeSeconds();
            foreach (var pair in values)
            {
                var raw = pair.Value.ValueKind == JsonValueKind.String ? pair.Value.GetString() : pair.Value.ToString();
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
                { series.Remove(pair.Key); continue; }
                if (!series.TryGetValue(pair.Key, out var points)) series[pair.Key] = points = [];
                // At most one point per second; repeated readings are still useful history.
                if (points.Count > 0 && points[^1].Second == second) points[^1] = (second, number);
                else { points.Add((second, number)); if (points.Count > HistoryLimit) points.RemoveAt(0); }
            }
            revisions[channel] = revisions.GetValueOrDefault(channel) + 1;
            snapshots.Remove(channel);
        }
        Updated?.Invoke(channel);
        return previous;
    }
}

// One consistent view of a channel: a reading and its graph always come from the same update.
public sealed record DataSnapshot(long Revision, IReadOnlyDictionary<string, JsonElement> Values,
    IReadOnlyDictionary<string, IReadOnlyList<double>> History)
{
    public static DataSnapshot Empty { get; } = new(0, FrozenDictionary<string, JsonElement>.Empty, FrozenDictionary<string, IReadOnlyList<double>>.Empty);
    public IReadOnlyList<double> ReadHistory(string key, int count) => History.TryGetValue(key, out var points) ? points.TakeLast(count).ToArray() : [];
}
