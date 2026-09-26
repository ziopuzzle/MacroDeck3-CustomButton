using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ziopuzzle.CustomButton;

// A bounded expression grammar: data lookup, comparisons and Boolean composition only.
public static class DisplayCondition
{
    private static readonly Regex Token = new("\\G\\s*(?:(?<number>-?(?:[0-9]+(?:\\.[0-9]+)?)(?:[eE][+-]?[0-9]+)?)|(?<text>'[^']*'|\"[^\"]*\")|(?<id>[A-Za-z_][A-Za-z0-9_.-]*)|(?<op>>=|<=|==|!=|&&|\\|\\||[><!()]))", RegexOptions.CultureInvariant);
    public static bool Evaluate(string expression, IReadOnlyDictionary<string, JsonElement> values)
    {
        if (expression.Length > 1024) throw new FormatException("Conditions must not exceed 1024 characters.");
        var tokens = new List<(string Kind, string Text)>();
        for (int pos = 0; pos < expression.Length;)
        {
            if (string.IsNullOrWhiteSpace(expression[pos..])) break;
            var match = Token.Match(expression, pos);
            if (!match.Success) throw new FormatException("Cannot parse condition: " + expression[pos..]);
            var kind = new[] { "number", "text", "id", "op" }.First(k => match.Groups[k].Success);
            tokens.Add((kind, match.Groups[kind].Value)); pos = match.Index + match.Length;
            if (tokens.Count > 128) throw new FormatException("The condition is too complex.");
        }
        int at = 0, depth = 0;
        bool Take(params string[] names)
        {
            if (at < tokens.Count && tokens[at].Kind is "id" or "op" && names.Contains(tokens[at].Text)) { at++; return true; }
            return false;
        }
        object? Atom()
        {
            if (++depth > 16) throw new FormatException("Conditions must not exceed 16 nesting levels.");
            try
            {
                if (Take("(")) { var result = Or(); if (!Take(")")) throw new FormatException("The condition has an unclosed parenthesis."); return result; }
                if (at == tokens.Count) throw new FormatException("The condition is missing a value.");
                var t = tokens[at++];
                if (t.Kind == "number")
                {
                    if (!double.TryParse(t.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || !double.IsFinite(n)) throw new FormatException("A number in the condition is out of range.");
                    return n;
                }
                if (t.Kind == "text") return t.Text[1..^1];
                if (t.Kind != "id") throw new FormatException("Invalid condition value.");
                if (t.Text == "true") return true;
                if (t.Text == "false") return false;
                if (t.Text == "null") return null;
                return values.TryGetValue(t.Text, out var value) ? value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString(), JsonValueKind.Number => value.GetDouble(),
                    JsonValueKind.True => true, JsonValueKind.False => false, _ => null
                } : null;
            }
            finally { depth--; }
        }
        bool Comparison()
        {
            var left = Atom();
            if (at == tokens.Count || tokens[at].Kind != "op" || !new[] { "==", "!=", ">", ">=", "<", "<=" }.Contains(tokens[at].Text)) return Truth(left);
            var op = tokens[at++].Text; var right = Atom();
            if (op is "==" or "!=")
            {
                var equal = left == null || right == null ? left == right
                    : left is bool lb && right is string rs && bool.TryParse(rs, out var rb) ? lb == rb
                    : right is bool rb2 && left is string ls && bool.TryParse(ls, out var lb2) ? lb2 == rb2
                    : Numeric(left, out var a) && Numeric(right, out var b) ? a == b : Equals(left, right);
                return op == "==" ? equal : !equal;
            }
            if (!Numeric(left, out var l) || !Numeric(right, out var r)) return false;
            return op switch { ">" => l > r, ">=" => l >= r, "<" => l < r, _ => l <= r };
        }
        bool Not()
        {
            bool negate = false;
            while (Take("!", "not")) negate = !negate;
            var value = Comparison(); return negate ? !value : value;
        }
        bool And() { var value = Not(); while (Take("&&", "and")) { var other = Not(); value &= other; } return value; }
        bool Or() { var value = And(); while (Take("||", "or")) { var other = And(); value |= other; } return value; }
        var answer = Or();
        if (at != tokens.Count) throw new FormatException("The condition contains an unsupported operator.");
        return answer;
    }
    private static bool Numeric(object? value, out double number)
        => double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number);
    private static bool Truth(object? value) => value switch
    {
        null => false, bool b => b, string s when bool.TryParse(s, out var b) => b,
        _ => Numeric(value, out var n) ? n != 0 : value is string s && s.Length > 0
    };
}
