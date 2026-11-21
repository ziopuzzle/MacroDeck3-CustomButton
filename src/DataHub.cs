using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Collections.Frozen;

namespace Ziopuzzle.CustomButton;

// Channel values and history are transient. Shared defaults come from the host's config entry.
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
    private DataSnapshot defaults = DataSnapshot.Empty;
    // Subscribers only enqueue work. Notify outside the data lock so rendering never blocks writers.
    internal event Action<string?>? Updated;
    internal void SetSharedValues(string json) => SetSharedValues(ParseValues(json));
    internal void SetSharedValues(Dictionary<string, JsonElement> next)
    {
        lock (gate)
        {
            var previous = defaults.SharedValues;
            // Bindings can display raw JSON, including the difference between 1 and 1.00.
            if (previous.Count == next.Count && next.All(p => previous.TryGetValue(p.Key, out var old) && old.GetRawText() == p.Value.GetRawText())) return;
            defaults = DataSnapshot.Empty with { Revision = defaults.Revision + 1, SharedValues = next.ToFrozenDictionary(StringComparer.Ordinal) };
            snapshots.Clear();
        }
        Updated?.Invoke(null);
    }
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
            var values = defaults.Resolve(initialValues);
            foreach (var pair in channels[channel]) values[pair.Key] = pair.Value;
            if (!history.TryGetValue(channel, out var series)) history[channel] = series = new(StringComparer.Ordinal);
            bool changed = false;
            foreach (var pair in values)
            {
                if (TryHistoryNumber(pair.Value, out var number))
                    changed |= RecordHistory(series, pair.Key, second, number, replaceCurrentSecond: false);
            }
            sampledSeconds[channel] = second;
            if (changed) InvalidateSnapshot(channel);
        }
    }
    public long Revision(string channel) { lock (gate) return defaults.Revision + revisions.GetValueOrDefault(channel); }
    public DataSnapshot Snapshot(string channel)
    {
        lock (gate)
        {
            if (snapshots.TryGetValue(channel, out var snapshot)) return snapshot;
            if (!channels.TryGetValue(channel, out var data)) return defaults;
            return snapshots[channel] = new DataSnapshot(defaults.Revision + revisions.GetValueOrDefault(channel),
                data.ToFrozenDictionary(StringComparer.Ordinal),
                history.TryGetValue(channel, out var series)
                    ? series.ToFrozenDictionary(p => p.Key, p => (IReadOnlyList<double>)Array.AsReadOnly(p.Value.Select(v => v.Value).ToArray()), StringComparer.Ordinal)
                    : FrozenDictionary<string, IReadOnlyList<double>>.Empty) { SharedValues = defaults.SharedValues };
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
    internal void Edit(string channel, Action<Dictionary<string, JsonElement>> edit)
        => UpdateCore(channel, new Dictionary<string, JsonElement>(), false, null, edit: edit);
    private JsonElement? UpdateCore(string channel, IReadOnlyDictionary<string, JsonElement> values, bool replace, string? previousKey, Action<Dictionary<string, JsonElement>>? edit = null)
    {
        ValidateName(channel);
        JsonElement? previous = null;
        lock (gate)
        {
            if (previousKey != null && channels.TryGetValue(channel, out var prior) && prior.TryGetValue(previousKey, out var oldValue)) previous = oldValue;
            if (!channels.ContainsKey(channel) && channels.Count >= 256) throw new FormatException("The maximum of 256 channels has been reached.");
            var merged = !replace && channels.TryGetValue(channel, out var old) ? new Dictionary<string, JsonElement>(old, StringComparer.Ordinal) : new(StringComparer.Ordinal);
            foreach (var pair in values) { ValidateName(pair.Key); merged[pair.Key] = pair.Value.Clone(); }
            if (edit != null)
            {
                var beforeEdit = new Dictionary<string, JsonElement>(merged, StringComparer.Ordinal);
                edit(merged);
                values = merged.Where(pair => !beforeEdit.TryGetValue(pair.Key, out var priorValue)
                    || priorValue.GetRawText() != pair.Value.GetRawText()).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                replace = true;
            }
            // Revalidate the combined payload, so small updates cannot grow it without bound.
            ParseValues(JsonSerializer.Serialize(merged));
            channels[channel] = merged;
            if (!history.TryGetValue(channel, out var series)) history[channel] = series = new(StringComparer.Ordinal);
            if (replace) foreach (var key in series.Keys.Except(merged.Keys).ToArray()) series.Remove(key);
            var second = clock.GetUtcNow().ToUnixTimeSeconds();
            foreach (var pair in values)
            {
                if (!TryHistoryNumber(pair.Value, out var number))
                { series.Remove(pair.Key); continue; }
                RecordHistory(series, pair.Key, second, number, replaceCurrentSecond: true);
            }
            InvalidateSnapshot(channel);
        }
        Updated?.Invoke(channel);
        return previous;
    }

    private static bool TryHistoryNumber(JsonElement value, out double number)
    {
        var raw = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number);
    }

    private static bool RecordHistory(Dictionary<string, List<(long Second, double Value)>> series,
        string key, long second, double value, bool replaceCurrentSecond)
    {
        if (!series.TryGetValue(key, out var points)) series[key] = points = [];
        // Sampling preserves this second's reading; an explicit write replaces it.
        if (points.Count > 0 && points[^1].Second == second)
        {
            if (!replaceCurrentSecond) return false;
            points[^1] = (second, value);
        }
        else
        {
            points.Add((second, value));
            if (points.Count > HistoryLimit) points.RemoveAt(0);
        }
        return true;
    }

    // Both callers hold gate, so values, history and cached snapshots advance together.
    private void InvalidateSnapshot(string channel)
    {
        revisions[channel] = revisions.GetValueOrDefault(channel) + 1;
        snapshots.Remove(channel);
    }
}

// One consistent view of a channel: a reading and its graph always come from the same update.
public sealed record DataSnapshot(long Revision, IReadOnlyDictionary<string, JsonElement> Values,
    IReadOnlyDictionary<string, IReadOnlyList<double>> History)
{
    public static DataSnapshot Empty { get; } = new(0, FrozenDictionary<string, JsonElement>.Empty, FrozenDictionary<string, IReadOnlyList<double>>.Empty);
    public IReadOnlyDictionary<string, JsonElement> SharedValues { get; init; } = FrozenDictionary<string, JsonElement>.Empty;
    public Dictionary<string, JsonElement> Resolve(IReadOnlyDictionary<string, JsonElement> initial)
    {
        var result = new Dictionary<string, JsonElement>(initial, StringComparer.Ordinal);
        foreach (var pair in SharedValues) result.TryAdd(pair.Key, pair.Value);
        foreach (var pair in Values) result[pair.Key] = pair.Value;
        return result;
    }
    public IReadOnlyList<double> ReadHistory(string key, int count) => History.TryGetValue(key, out var points) ? points.TakeLast(count).ToArray() : [];
}
