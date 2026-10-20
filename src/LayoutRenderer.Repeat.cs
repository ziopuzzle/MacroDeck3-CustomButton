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
    private sealed class FollowState
    {
        public string Request = "";
        public int Phase;
        public double Since;
        public bool AwaitingDelivery = true;
    }
    private readonly Dictionary<string, FollowState> scrollFollowing = new();
    private readonly Dictionary<string, int> scrollLimits = new();
    private readonly HashSet<string> seenScrolls = new();
    internal Func<double> ScrollClock { get; set; } = () => Environment.TickCount64;
    internal bool RequireScrollDelivery { get; set; }
    internal void ScrollDelivered()
    {
        foreach (var state in scrollFollowing.Values.Where(s => s.AwaitingDelivery))
        { state.AwaitingDelivery = false; state.Since = ScrollClock(); }
    }
    public bool ScrollFollowActive => scrollFollowing.Values.Any(s => s.Phase < 2);

    // Experimental end-follow workaround: keep List/Repeat identities stable throughout.
    private string? FollowScroll(XElement node, Dictionary<string, string> attributes, IReadOnlyDictionary<string, JsonElement> values, bool repeated)
    {
        var id = (string)node.Attribute("id")!;
        seenScrolls.Add(id);
        var key = attributes.GetValueOrDefault("followKey", "").Trim();
        if (key.Length == 0) { scrollFollowing.Remove(id); return null; }
        var children = node.Elements().Where(e => e.Name != "style").ToArray();
        if (repeated || children.Length != 1 || children[0].Name != "repeat")
            throw new FormatException("Scroll follow needs one direct Repeat child and cannot be nested inside Repeat.");
        if (Expand(attributes.GetValueOrDefault("direction", "vertical"), values) != "vertical")
            throw new FormatException("Scroll follow supports only vertical scrolling.");
        key = RepeatDataName(key);
        var repeat = children[0];
        var items = RepeatItems((string?)repeat.Attribute("items") ?? "", (string?)repeat.Attribute("itemKey") ?? "", (string)repeat.Attribute("id")!, values, null).ToArray();
        if (!values.TryGetValue(key, out var raw) || !double.TryParse(raw.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var target) || !double.IsFinite(target) || target < 0 || target != Math.Truncate(target) || items.Length == 0)
        { scrollFollowing.Remove(id); return "start"; }
        int Number(string name, int fallback, int min, int max)
        {
            var text = Expand(attributes.GetValueOrDefault(name, fallback.ToString(CultureInfo.InvariantCulture)), values);
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) || number < min || number > max)
                throw new FormatException($"{name} must be an integer from {min} to {max}.");
            return number;
        }
        var offset = Number("followOffset", 0, -256, 256);
        var delay = Number("followDelayMs", 300, 0, 5000);
        var request = key + ":" + target.ToString(CultureInfo.InvariantCulture) + ":" + offset;
        var now = ScrollClock();
        if (!scrollFollowing.TryGetValue(id, out var state) || state.Request != request)
            scrollFollowing[id] = state = new FollowState { Request = request, Since = now };
        else if (state.Phase < 2 && (!RequireScrollDelivery || !state.AwaitingDelivery) && now - state.Since >= delay)
        { state.Phase++; state.Since = now; state.AwaitingDelivery = true; }
        scrollLimits[(string)repeat.Attribute("id")!] = state.Phase switch
        {
            0 => 0,
            1 => (int)Math.Clamp(target + offset + 1, 1, items.Length),
            _ => int.MaxValue
        };
        return state.Phase < 2 ? "end" : "start";
    }
    private sealed record RepeatAliases(string Item, string Index, string Key)
    {
        public string[] Names => [Item, Index, Key];
        public static RepeatAliases Read(XElement node)
        {
            string Name(string attribute, string fallback) => (string?)node.Attribute(attribute) is { Length: > 0 } name ? name : fallback;
            var result = new RepeatAliases(Name("itemAlias", "item"), Name("indexAlias", "index"), Name("keyAlias", "itemKey"));
            if (result.Names.Any(n => !Regex.IsMatch(n, @"\A[A-Za-z_][A-Za-z0-9_]*\z")) || result.Names.Distinct(StringComparer.Ordinal).Count() != 3)
                throw new FormatException("Repeat aliases must be distinct literal names containing letters, digits or underscores, starting with a letter or underscore.");
            return result;
        }
    }
    private sealed record RepeatScope(string Token, RepeatedItemInput Item, string[] ReadOnlyNames)
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
                var first = RepeatItems((string)repeat.Attribute("items")!, (string?)repeat.Attribute("itemKey") ?? "", (string)repeat.Attribute("id")!, values, scope, RepeatAliases.Read(repeat)).FirstOrDefault();
                if (first.Values == null) break;
                values = first.Values; scope = first.Scope;
            }
            catch (FormatException) { break; } // Suggestions must not block editing invalid data.
        }
        var names = scope?.ReadOnlyNames ?? ancestors.SelectMany(n => RepeatAliases.Read(n).Names).Distinct().ToArray();
        return names.Concat(values.Keys.Where(k => names.Any(n => k.StartsWith(n + ".", StringComparison.Ordinal)))).Distinct().ToArray();
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
        string source, string keyPath, string repeatId, IReadOnlyDictionary<string, JsonElement> values, RepeatScope? parent, RepeatAliases? aliases = null)
    {
        aliases ??= new("item", "index", "itemKey");
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
            var scoped = values.Where(p => !aliases.Names.Any(n => p.Key == n || p.Key.StartsWith(n + ".", StringComparison.Ordinal)))
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            void Add(string path, JsonElement value)
            {
                scoped[path] = value;
                if (value.ValueKind == JsonValueKind.Object)
                    foreach (var property in value.EnumerateObject()) Add(path + "." + property.Name, property.Value);
            }
            Add(aliases.Item, item);
            scoped[aliases.Index] = JsonSerializer.SerializeToElement(index);
            scoped[aliases.Key] = JsonSerializer.SerializeToElement(itemKey);
            var token = Hash((parent?.Token ?? "") + ":" + repeatId + ":" + itemKey);
            yield return (scoped, new RepeatScope(token, new(repeatId, itemKey, index, item.GetRawText()), (parent?.ReadOnlyNames ?? []).Concat(aliases.Names).Distinct().ToArray()));
            index++;
        }
    }
}
