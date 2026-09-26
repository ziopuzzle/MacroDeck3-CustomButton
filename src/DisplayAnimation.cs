namespace Ziopuzzle.CustomButton;

/// <summary>Per-view, monotonic transitions. Values are presentation-only, never written to data/history.</summary>
public sealed class DisplayAnimation(TimeProvider? timeProvider = null)
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private sealed record Track(double From, double To, long Start, double Duration, string Easing);
    private readonly Dictionary<string, Track> tracks = new(StringComparer.Ordinal);
    private readonly HashSet<string> seen = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> colorTargets = new(StringComparer.Ordinal);
    private long now;
    public bool IsActive { get; private set; }
    public DisplayAnimation Copy()
    {
        var copy = new DisplayAnimation(clock) { now = now, IsActive = IsActive };
        foreach (var pair in tracks) copy.tracks.Add(pair.Key, pair.Value);
        foreach (var pair in colorTargets) copy.colorTargets.Add(pair.Key, pair.Value);
        return copy;
    }
    public void BeginFrame() { now = clock.GetTimestamp(); seen.Clear(); IsActive = false; }
    public void EndFrame()
    {
        foreach (var key in tracks.Keys.Where(k => !seen.Contains(k)).ToArray()) tracks.Remove(key);
        foreach (var key in colorTargets.Keys.Where(k => !seen.Contains(k + ".0")).ToArray()) colorTargets.Remove(key);
    }
    public void Reset() { tracks.Clear(); colorTargets.Clear(); seen.Clear(); IsActive = false; }
    public string Color(string key, string rgb, double duration, string easing, string space)
    {
        key += "." + space;
        var target = ColorInterpolation.Decode(rgb, space);
        if (space == "hsv" && tracks.TryGetValue(key + ".0", out var hue))
        {
            if (colorTargets.GetValueOrDefault(key) == rgb) target[0] = hue.To;
            else
            {
                var currentHue = Sample(hue);
                // Greys have no hue: retain the coloured endpoint's hue rather than visiting red.
                if (target[1] == 0) target[0] = currentHue;
                else if (tracks.TryGetValue(key + ".1", out var saturation) && Sample(saturation) == 0)
                    tracks[key + ".0"] = new(target[0], target[0], now, duration, easing);
                else target[0] = currentHue + ((target[0] - currentHue) % 360 + 540) % 360 - 180;
            }
        }
        colorTargets[key] = rgb;
        for (int i = 0; i < 3; i++) target[i] = Value(key + "." + i, target[i], duration, easing);
        return ColorInterpolation.Encode(target, space);
    }
    public double Value(string key, double target, double duration, string easing)
    {
        seen.Add(key);
        if (!tracks.TryGetValue(key, out var track) || duration == 0)
            tracks[key] = track = new(target, target, now, duration, easing);
        else if (track.To != target || track.Duration != duration || track.Easing != easing)
            tracks[key] = track = new(Sample(track), target, now, duration, easing);
        var value = Sample(track);
        if (track.From != track.To && clock.GetElapsedTime(track.Start, now).TotalMilliseconds < track.Duration) IsActive = true;
        return value;
    }
    private double Sample(Track track)
    {
        if (track.Duration == 0) return track.To;
        var t = Math.Clamp(clock.GetElapsedTime(track.Start, now).TotalMilliseconds / track.Duration, 0, 1);
        if (t >= 1) return track.To;
        if (t <= 0) return track.From;
        t = track.Easing switch { "ease-in" => t * t, "ease-out" => 1 - (1 - t) * (1 - t), "ease-in-out" => t * t * (3 - 2 * t), _ => t };
        return track.From + (track.To - track.From) * t;
    }
}
