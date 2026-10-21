using System.Globalization;
using System.Text.Json;

namespace Ziopuzzle.CustomButton;

// Typed, bounded transformations shared by XML calculations and conditions.
public static class DataFunctions
{
    public static bool IsSupported(string name) => name is "length" or "contains" or "startsWith" or "endsWith" or "replace" or "substring"
        or "lower" or "upper" or "trim" or "split" or "join" or "at" or "slice";
    public static void ValidateCount(string name, int count)
    {
        var valid = name switch
        {
            "length" or "lower" or "upper" or "trim" => count == 1,
            "contains" or "startsWith" or "endsWith" or "split" or "join" or "at" => count == 2,
            "replace" => count == 3,
            "substring" or "slice" => count is 2 or 3,
            _ => false
        };
        if (!valid) throw new FormatException($"Unknown function or incorrect argument count: {name}.");
    }
    public static JsonElement Evaluate(string name, IReadOnlyList<JsonElement> args)
    {
        ValidateCount(name, args.Count);
        if (args.Any(a => a.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)) return JsonSerializer.SerializeToElement<object?>(null);
        string Text(int i) => args[i].ValueKind == JsonValueKind.String ? args[i].GetString()! : throw new FormatException($"{name} requires a string at argument {i + 1}.");
        JsonElement[] Array(int i) => args[i].ValueKind == JsonValueKind.Array ? args[i].EnumerateArray().ToArray() : throw new FormatException($"{name} requires an array at argument {i + 1}.");
        int Integer(int i)
        {
            if (!DisplayData.TryNumber(args[i], out var n) || n < 0 || n > int.MaxValue || n != Math.Truncate(n))
                throw new FormatException($"{name} requires a nonnegative integer at argument {i + 1}.");
            return (int)n;
        }
        object? result;
        switch (name)
        {
            case "length":
                result = args[0].ValueKind == JsonValueKind.Array ? args[0].GetArrayLength() : StringInfo.ParseCombiningCharacters(Text(0)).Length; break;
            case "contains": result = Text(0).Contains(Text(1), StringComparison.Ordinal); break;
            case "startsWith": result = Text(0).StartsWith(Text(1), StringComparison.Ordinal); break;
            case "endsWith": result = Text(0).EndsWith(Text(1), StringComparison.Ordinal); break;
            case "lower": result = Text(0).ToLowerInvariant(); break;
            case "upper": result = Text(0).ToUpperInvariant(); break;
            case "trim": result = Text(0).Trim(); break;
            case "replace":
                var source = Text(0); var search = Text(1); var replacement = Text(2);
                if (search.Length == 0) throw new FormatException("replace requires a nonempty search string.");
                var occurrences = 0;
                for (var i = 0; (i = source.IndexOf(search, i, StringComparison.Ordinal)) >= 0; i += search.Length) occurrences++;
                if ((long)source.Length + (long)occurrences * (replacement.Length - search.Length) > 16000)
                    throw new FormatException("Function result exceeds 16000 characters.");
                result = source.Replace(search, replacement, StringComparison.Ordinal); break;
            case "substring":
                var str = Text(0); var offsets = StringInfo.ParseCombiningCharacters(str);
                var start = Math.Min(Integer(1), offsets.Length);
                var end = (int)Math.Min(offsets.Length, (long)start + (args.Count == 3 ? Integer(2) : offsets.Length));
                result = str[(start == offsets.Length ? str.Length : offsets[start])..(end == offsets.Length ? str.Length : offsets[end])]; break;
            case "split":
                var separator = Text(1);
                if (separator.Length == 0) throw new FormatException("split requires a nonempty separator.");
                var pieces = Text(0).Split(separator, StringSplitOptions.None);
                if (pieces.Length > 256) throw new FormatException("split must not produce more than 256 items.");
                result = pieces; break;
            case "join":
                var array = Array(0); var delimiter = Text(1);
                var strings = array.Select(v => v.ValueKind switch
                {
                    JsonValueKind.String => v.GetString()!, JsonValueKind.Number => v.GetRawText(),
                    JsonValueKind.True => "true", JsonValueKind.False => "false", JsonValueKind.Null => "",
                    _ => throw new FormatException("join accepts only strings, numbers, booleans or null array items.")
                }).ToArray();
                if (strings.Sum(s => (long)s.Length) + (long)Math.Max(0, strings.Length - 1) * delimiter.Length > 16000)
                    throw new FormatException("Function result exceeds 16000 characters.");
                result = string.Join(delimiter, strings); break;
            case "at":
                var items = Array(0); var index = Integer(1);
                result = index < items.Length ? items[index] : null; break;
            case "slice":
                var data = Array(0); var skip = Integer(1);
                result = data.Skip(skip).Take(args.Count == 3 ? Integer(2) : data.Length).ToArray(); break;
            default: throw new FormatException($"Unknown function: {name}.");
        }
        var json = JsonSerializer.SerializeToElement(result);
        if (json.GetRawText().Length > 16000) throw new FormatException("Function result exceeds 16000 JSON characters.");
        return json;
    }
}
