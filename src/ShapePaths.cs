using System.Globalization;
using System.Text.RegularExpressions;

namespace Ziopuzzle.CustomButton;

/// <summary>Normalized absolute SVG paths accepted by the public ui.shape component.</summary>
public static class ShapePaths
{
    private static string F(double value) => value.ToString("0.########", CultureInfo.InvariantCulture);
    public static string Polygon(string text) => "M" + string.Join(" L", ParsePoints(text).Select(p => F(p.X) + " " + F(p.Y))) + " Z";
    public static (double X, double Y)[] ParsePoints(string text)
    {
        double Coordinate(string raw)
        {
            raw = raw.Trim(); var percent = raw.EndsWith('%');
            if (!double.TryParse(percent ? raw[..^1] : raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) throw new FormatException("Specify coordinates as 0.2 or 20%.");
            value = percent ? value / 100 : value;
            if (!double.IsFinite(value) || value < 0 || value > 1) throw new FormatException("Coordinates must be between 0 and 100%.");
            return value;
        }
        var entries = text.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (entries.Length is < 3 or > 64) throw new FormatException("Separate 3–64 polygon points with semicolons.");
        return entries.Select(entry => { var xy = entry.Split(','); if (xy.Length != 2) throw new FormatException("Specify each point as x,y."); return (Coordinate(xy[0]), Coordinate(xy[1])); }).ToArray();
    }
    public static string Sector(double cx, double cy, double radius, double start, double sweep)
    {
        var angles = new List<double> { start, start + sweep };
        // Arc extrema only occur at its endpoints or at crossed cardinal angles.
        for (var angle = Math.Ceiling(Math.Min(start, start + sweep) / 90) * 90;
             angle <= Math.Max(start, start + sweep); angle += 90) angles.Add(angle);
        bool Outside(double x, double y) => x < -1e-10 || x > 1 + 1e-10 || y < -1e-10 || y > 1 + 1e-10;
        if (Outside(cx, cy) || angles.Any(a => Outside(cx + radius * Math.Cos(a * Math.PI / 180), cy + radius * Math.Sin(a * Math.PI / 180))))
            throw new FormatException("Keep the sector's visible arc and center within 0–100%.");
        string Point(double angle) => F(cx + radius * Math.Cos(angle * Math.PI / 180)) + " " + F(cy + radius * Math.Sin(angle * Math.PI / 180));
        var direction = sweep >= 0 ? 1 : 0;
        string Arc(double angle, double part) => $" A{F(radius)} {F(radius)} 0 {(Math.Abs(part) > 180 ? 1 : 0)} {direction} {Point(angle)}";
        // Two arcs avoid SVG's coincident-endpoint full-circle ambiguity. No radial seam for a full circle.
        if (Math.Abs(sweep) == 360) return "M" + Point(start) + Arc(start + sweep / 2, sweep / 2) + Arc(start + sweep, sweep / 2) + " Z";
        return $"M{F(cx)} {F(cy)} L{Point(start)}" + Arc(start + sweep, sweep) + " Z";
    }
    public static string Validate(string path)
    {
        if (path.Length > 4096) throw new FormatException("Paths must not exceed 4096 characters.");
        var rest = path.Trim();
        void Invalid() => throw new FormatException("Start paths with M. Use absolute M L H V C Q A Z commands with the correct number of arguments.");
        if (!rest.StartsWith('M')) Invalid();
        while (rest.Length > 0)
        {
            var command = rest[0];
            var arity = command switch { 'M' or 'L' => 2, 'H' or 'V' => 1, 'C' => 6, 'Q' => 4, 'A' => 7, 'Z' => 0, _ => -1 };
            if (arity < 0) Invalid();
            rest = rest[1..]; var numbers = new List<double>();
            while (true)
            {
                rest = rest.TrimStart(' ', '\t', '\n', '\r', ',');
                var match = Regex.Match(rest, @"^[+-]?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?");
                if (!match.Success) break;
                if (!double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || !double.IsFinite(n)) Invalid();
                numbers.Add(n); rest = rest[match.Length..];
            }
            if (arity == 0 ? numbers.Count != 0 : numbers.Count == 0 || numbers.Count % arity != 0) Invalid();
            if (command == 'A') for (int i = 0; i < numbers.Count; i += 7)
                if (numbers[i] < 0 || numbers[i + 1] < 0 || numbers[i + 3] is not (0 or 1) || numbers[i + 4] is not (0 or 1)) Invalid();
        }
        return path;
    }
}
