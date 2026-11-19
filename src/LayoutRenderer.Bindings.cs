using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ziopuzzle.CustomButton;

public sealed partial class LayoutRenderer
{
    private static readonly Regex Slot = new(@"\{\{\s*([A-Za-z0-9_.-]+)\s*(?::([^{}]{1,32}))?\s*\}\}", RegexOptions.CultureInvariant);

    public static string Expand(string text, IReadOnlyDictionary<string, JsonElement> values)
    {
        var output = new System.Text.StringBuilder();
        for (int position = 0; position < text.Length;)
        {
            var open = text.IndexOf("{{", position, StringComparison.Ordinal);
            if (open < 0) { output.Append(text[position..]); break; }
            output.Append(text[position..open]);
            var body = open + 2;
            while (body < text.Length && char.IsWhiteSpace(text[body])) body++;
            if (body < text.Length && text[body] == '=')
            {
                var close = -1; var separator = -1; char quote = '\0';
                for (var i = body + 1; i < text.Length; i++)
                {
                    var c = text[i];
                    if (quote != '\0') { if (c == '\\') i++; else if (c == quote) quote = '\0'; }
                    else if (c is '\'' or '"') quote = c;
                    else if (c == ':' && separator < 0) separator = i;
                    else if (c == '}' && i + 1 < text.Length && text[i + 1] == '}') { close = i; break; }
                }
                if (close < 0) { output.Append(text[open..]); break; }
                var result = DisplayMath.Evaluate(text[(body + 1)..(separator < 0 ? close : separator)], values);
                output.Append(separator < 0 ? result : DisplayFormat.Apply(result, text[(separator + 1)..close]));
                position = close + 2;
            }
            else
            {
                var match = Slot.Match(text, open);
                if (!match.Success || match.Index != open) { output.Append("{{"); position = open + 2; continue; }
                var raw = values.TryGetValue(match.Groups[1].Value, out var value) && value.ValueKind != JsonValueKind.Null
                    ? value.ValueKind == JsonValueKind.String ? value.GetString()! : value.ToString() : "—";
                output.Append(match.Groups[2].Success ? DisplayFormat.Apply(raw, match.Groups[2].Value) : raw);
                position = open + match.Length;
            }
        }
        return output.ToString();
    }

}
