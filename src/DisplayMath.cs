using System.Globalization;
using System.Text.Json;

namespace Ziopuzzle.CustomButton;

/// <summary>Bounded arithmetic only: no script execution or access outside display data.</summary>
public sealed class DisplayMath
{
    private readonly string text;
    private readonly IReadOnlyDictionary<string, JsonElement> values;
    private int position, depth;
    private bool missing;
    private DisplayMath(string text, IReadOnlyDictionary<string, JsonElement> values) { this.text = text; this.values = values; }
    public static string Evaluate(string expression, IReadOnlyDictionary<string, JsonElement> values)
    {
        if (expression.Length > 256) throw new FormatException("Expressions must not exceed 256 characters.");
        var parser = new DisplayMath(expression, values);
        var result = parser.Sum(); parser.Space();
        if (parser.position != expression.Length) throw new FormatException("The expression contains an unsupported symbol.");
        if (parser.missing) return "—";
        if (!double.IsFinite(result)) throw new FormatException("The result is not finite. Check for division by zero.");
        return result.ToString("G", CultureInfo.InvariantCulture);
    }
    private void Space() { while (position < text.Length && char.IsWhiteSpace(text[position])) position++; }
    private bool Take(char c) { Space(); if (position >= text.Length || text[position] != c) return false; position++; return true; }
    private double Sum() { var n = Product(); while (true) { if (Take('+')) n += Product(); else if (Take('-')) n -= Product(); else return n; } }
    private double Product() { var n = Atom(); while (true) { if (Take('*')) n *= Atom(); else if (Take('/')) n /= Atom(); else if (Take('%')) n %= Atom(); else return n; } }
    private double Atom()
    {
        if (++depth > 32) throw new FormatException("The expression is nested too deeply.");
        try
        {
            if (Take('+')) return Atom(); if (Take('-')) return -Atom();
            if (Take('(')) { var n = Sum(); Require(')'); return n; }
            Space(); var start = position;
            if (position < text.Length && (char.IsDigit(text[position]) || text[position] == '.'))
            {
                while (position < text.Length && (char.IsDigit(text[position]) || text[position] == '.')) position++;
                if (position < text.Length && text[position] is 'e' or 'E')
                {
                    position++;
                    if (position < text.Length && text[position] is '+' or '-') position++;
                    while (position < text.Length && char.IsAsciiDigit(text[position])) position++;
                }
                if (!double.TryParse(text[start..position], NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || !double.IsFinite(n)) throw new FormatException("Invalid number in the expression.");
                return n;
            }
            while (position < text.Length && (char.IsAsciiLetterOrDigit(text[position]) || text[position] is '_' or '.')) position++;
            if (start == position) throw new FormatException("The expression needs a number or data name.");
            var name = text[start..position];
            if (Take('('))
            {
                var args = new List<double>();
                if (!Take(')'))
                {
                    do { if (args.Count == 3) throw new FormatException("Functions accept at most three arguments."); args.Add(Sum()); } while (Take(','));
                    Require(')');
                }
                return Function(name, args);
            }
            if (!values.TryGetValue(name, out var value) || value.ValueKind == JsonValueKind.Null) { missing = true; return 0; }
            if (!double.TryParse(value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number)) throw new FormatException($"{name} is not a valid number for calculation.");
            return number;
        }
        finally { depth--; }
    }
    private void Require(char c) { if (!Take(c)) throw new FormatException($"The expression requires {c}."); }
    private static double Function(string name, List<double> a)
    {
        switch (name, a.Count)
        {
            case ("floor", 1): return Math.Floor(a[0]);
            case ("ceil", 1): return Math.Ceiling(a[0]);
            case ("trunc", 1): return Math.Truncate(a[0]);
            case ("abs", 1): return Math.Abs(a[0]);
            case ("sqrt", 1): return Math.Sqrt(a[0]);
            case ("sin", 1): return Math.Sin(a[0]);
            case ("cos", 1): return Math.Cos(a[0]);
            case ("round", 1): return Math.Round(a[0], MidpointRounding.AwayFromZero);
            case ("round", 2):
                if (!double.IsFinite(a[1]) || a[1] < 0 || a[1] > 6 || a[1] != Math.Truncate(a[1]))
                    throw new FormatException("round digits must be an integer between 0 and 6.");
                return Math.Round(a[0], (int)a[1], MidpointRounding.AwayFromZero);
            case ("min", 2): return Math.Min(a[0], a[1]);
            case ("max", 2): return Math.Max(a[0], a[1]);
            case ("pow", 2): return Math.Pow(a[0], a[1]);
            case ("lerp", 3): return a[0] + (a[1] - a[0]) * a[2];
            case ("clamp", 3):
                if (a[1] > a[2]) throw new FormatException("The clamp minimum exceeds its maximum.");
                return Math.Clamp(a[0], a[1], a[2]);
            default: throw new FormatException($"Unknown function or incorrect argument count: {name}.");
        }
    }
}
