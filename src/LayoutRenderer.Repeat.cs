using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Ziopuzzle.CustomButton;

public sealed partial class LayoutRenderer
{
    private int expandedComponents;
    private sealed record RepeatScope(string Token, RepeatedItemInput Item)
    {
        public string NodeKey(string id) => "r" + Hash(Token + ":" + id)[..31];
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    internal static string[] RepeatPreviewKeys(string layout, string selectedId, IReadOnlyDictionary<string, JsonElement> values)
    {
        var ancestors = new LayoutDocument(layout).Find(selectedId).Ancestors().Where(n => n.Name == "repeat").Reverse().ToArray();
        if (ancestors.Length == 0) return [];
        RepeatScope? scope = null;
        foreach (var repeat in ancestors)
        {
            try
            {
                var first = RepeatItems((string)repeat.Attribute("items")!, (string?)repeat.Attribute("itemKey") ?? "", (string)repeat.Attribute("id")!, values, scope).FirstOrDefault();
                if (first.Values == null) break;
                values = first.Values; scope = first.Scope;
            }
            catch (FormatException) { break; } // Suggestions must not block editing invalid data.
        }
        return new[] { "item", "index", "itemKey" }.Concat(values.Keys.Where(k => k.StartsWith("item.", StringComparison.Ordinal))).Distinct().ToArray();
    }

    private static string RepeatDataName(string source)
    {
        source = source.Trim();
        var binding = Slot.Match(source);
        if (binding.Success && binding.Index == 0 && binding.Length == source.Length && !binding.Groups[2].Success)
            source = binding.Groups[1].Value;
        if (!Regex.IsMatch(source, @"\A[A-Za-z0-9_.-]+\z"))
            throw new FormatException("Repeat items needs a data name or a single binding, such as {{tracks}}.");
        return source;
    }

    private static bool ItemProperty(JsonElement item, string path, out JsonElement value)
    {
        value = item;
        foreach (var part in path.Split('.'))
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(part, out var child)) { value = default; return false; }
            value = child;
        }
        return true;
    }

    // Each item gets a private read-only render scope; Channel data is never polluted
    // with item/index aliases. Nested repeats replace the nearest item scope.
    private static IEnumerable<(IReadOnlyDictionary<string, JsonElement> Values, RepeatScope Scope)> RepeatItems(
        string source, string keyPath, string repeatId, IReadOnlyDictionary<string, JsonElement> values, RepeatScope? parent)
    {
        var name = RepeatDataName(source);
        if (!values.TryGetValue(name, out var array) || array.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) yield break;
        if (array.ValueKind != JsonValueKind.Array) throw new FormatException($"Repeat source '{name}' must be a JSON array, not {DisplayData.TypeOf(array)}.");
        if (array.GetArrayLength() > LayoutLimits.RepeatItems)
            throw new FormatException($"Repeat source '{name}' exceeds {LayoutLimits.RepeatItems} items. Pass a smaller array.");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            var itemKey = index.ToString(CultureInfo.InvariantCulture);
            if (keyPath.Length > 0)
            {
                if (!ItemProperty(item, keyPath, out var key) || key.ValueKind is not (JsonValueKind.String or JsonValueKind.Number))
                    throw new FormatException($"Repeat item {index} needs a string or number at itemKey '{keyPath}'.");
                itemKey = key.ToString();
                if (itemKey.Length == 0) throw new FormatException($"Repeat item {index} has an empty itemKey '{keyPath}'.");
            }
            if (!keys.Add(itemKey)) throw new FormatException($"Duplicate repeat item key '{itemKey}' at index {index}.");
            var scoped = values.Where(p => p.Key != "item" && !p.Key.StartsWith("item.", StringComparison.Ordinal) && p.Key is not ("index" or "itemKey"))
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            void Add(string path, JsonElement value)
            {
                scoped[path] = value;
                if (value.ValueKind == JsonValueKind.Object)
                    foreach (var property in value.EnumerateObject()) Add(path + "." + property.Name, property.Value);
            }
            Add("item", item);
            scoped["index"] = JsonSerializer.SerializeToElement(index);
            scoped["itemKey"] = JsonSerializer.SerializeToElement(itemKey);
            var token = Hash((parent?.Token ?? "") + ":" + repeatId + ":" + itemKey);
            yield return (scoped, new RepeatScope(token, new(repeatId, itemKey, index, item.GetRawText())));
            index++;
        }
    }
}
