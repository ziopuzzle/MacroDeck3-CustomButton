using System.Globalization;
using System.Text.Json;

namespace Ziopuzzle.CustomButton;

/// <summary>Bounded numeric and colour expressions; no script execution or external data access.</summary>
public sealed class DisplayMath
{
    private readonly string text;
    private readonly IReadOnlyDictionary<string, JsonElement> values;
    private int position, depth;
    private bool missing;
    private readonly record struct Value(double Number, string? Text = null, string? Name = null)
    {
        public static implicit operator Value(double number) => new(number);
        public double Numeric()
        {
            if (Text == null) return Number;
            if (double.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n)) return n;
            throw new FormatException($"{Name ?? "This value"} is not a valid number for calculation.");
        }
        public static Value operator +(Value a, Value b) => a.Numeric() + b.Numeric();
        public static Value operator -(Value a, Value b) => a.Numeric() - b.Numeric();
        public static Value operator *(Value a, Value b) => a.Numeric() * b.Numeric();
        public static Value operator /(Value a, Value b) => a.Numeric() / b.Numeric();
        public static Value operator %(Value a, Value b) => a.Numeric() % b.Numeric();
        public static Value operator -(Value a) => -a.Numeric();
    }
    private DisplayMath(string text, IReadOnlyDictionary<string, JsonElement> values) { this.text = text; this.values = values; }
    public static string Evaluate(string expression, IReadOnlyDictionary<string, JsonElement> values)
    {
        if (expression.Length > 256) throw new FormatException("Expressions must not exceed 256 characters.");
        var parser = new DisplayMath(expression, values);
        var result = parser.Sum(); parser.Space();
        if (parser.position != expression.Length) throw new FormatException("The expression contains an unsupported symbol.");
        if (parser.missing) return "—";
        if (result.Text != null) return DisplayColor.Parse(result.Text).Hex;
        if (!double.IsFinite(result.Number)) throw new FormatException("The result is not finite. Check for division by zero.");
        return result.Number.ToString("G", CultureInfo.InvariantCulture);
    }
    private void Space() { while (position < text.Length && char.IsWhiteSpace(text[position])) position++; }
    private bool Take(char c) { Space(); if (position >= text.Length || text[position] != c) return false; position++; return true; }
    private Value Sum() { var n = Product(); while (true) { if (Take('+')) n += Product(); else if (Take('-')) n -= Product(); else return n; } }
    private Value Product() { var n = Atom(); while (true) { if (Take('*')) n *= Atom(); else if (Take('/')) n /= Atom(); else if (Take('%')) n %= Atom(); else return n; } }
    private Value Atom()
    {
        if (++depth > 32) throw new FormatException("The expression is nested too deeply.");
        try
        {
            if (Take('+')) return Atom().Numeric(); if (Take('-')) return -Atom();
            if (Take('(')) { var n = Sum(); Require(')'); return n; }
            Space(); var start = position;
            if (position < text.Length && text[position] is '\'' or '"')
            {
                var quote = text[position++]; var literal = new System.Text.StringBuilder();
                while (position < text.Length)
                {
                    var c = text[position++];
                    if (c == quote) return new(0, literal.ToString());
                    if (c == '\\')
                    {
                        if (position == text.Length || text[position] != quote && text[position] != '\\')
                            throw new FormatException("Only quote and backslash escapes are supported.");
                        c = text[position++];
                    }
                    literal.Append(c);
                }
                throw new FormatException("The expression has an unterminated string.");
            }
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
                if (name is "number" or "isNumber" or "isNumeric")
                {
                    Space(); var keyStart = position;
                    while (position < text.Length && (char.IsAsciiLetterOrDigit(text[position]) || text[position] is '_' or '.')) position++;
                    if (keyStart == position) throw new FormatException("The first argument must be a data name.");
                    values.TryGetValue(text[keyStart..position], out var raw);
                    var valid = DisplayData.TryNumber(raw, out var numeric);
                    if (name == "number")
                    {
                        Require(',');
                        var previousMissing = missing; missing = false;
                        var fallback = Sum(); var fallbackMissing = missing;
                        missing = previousMissing || (!valid && fallbackMissing);
                        Require(')'); return valid ? numeric : fallback.Numeric();
                    }
                    Require(')');
                    return (name == "isNumber" ? raw.ValueKind == JsonValueKind.Number : valid) ? 1 : 0;
                }
                var args = new List<Value>();
                if (!Take(')'))
                {
                    do { if (args.Count == 4) throw new FormatException("Functions accept at most four arguments."); args.Add(Sum()); } while (Take(','));
                    Require(')');
                }
                if (ColorFunctions.IsSupported(name))
                {
                    // Missing data propagates just as it does in numeric expressions.
                    if (missing) { ColorFunctions.ValidateCount(name, args.Count); return new(0, "transparent"); }
                    return new(0, ColorFunctions.Evaluate(name, args.Select(a => a.Text == null ? (object)a.Number : a.Text).ToArray()));
                }
                return Function(name, args.Select(a => a.Numeric()).ToList());
            }
            if (!values.TryGetValue(name, out var value) || value.ValueKind == JsonValueKind.Null) { missing = true; return 0; }
            if (value.ValueKind == JsonValueKind.String)
            {
                var raw = value.GetString()!;
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric) || !double.IsFinite(numeric)) return new(0, raw, name);
                return numeric;
            }
            if (!double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number)) throw new FormatException($"{name} is not a valid number for calculation.");
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
