using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MacroDeck.Ui.Components;
using MacroDeck.Ui.Dsl;

namespace Ziopuzzle.CustomButton;

public sealed record DesignField(string Key, string Label, object Default, string Group = "content",
    double Min = 0, double Max = 100, string? Choices = null, string? Modes = null, string Description = "");

// Legacy 0.2 settings and renderer, retained for saved-button compatibility only.
public sealed class DesignSettings
{
    public static IReadOnlyList<DesignField> Fields { get; } =
    [
        new("designPreset", "Display style", "gauge", Choices: "gauge:Value and bar|metric:Large text|dual:Two metrics|xml:XML"),
        new("designTitle", "Title", "CPU"),
        new("designKey", "Display data key", "value", Description: "Match the data key in Set display value. Enter a key such as value, not a variable expression."),
        new("designFallback", "Initial display value", "42"),
        new("designUnit", "Unit suffix", "%"),
        new("designSecondTitle", "Second title", "RAM", Modes: "dual"),
        new("designSecondKey", "Second data key", "ram", Modes: "dual"),
        new("designSecondFallback", "Second initial value", "64", Modes: "dual"),
        new("designSecondUnit", "Second unit", "%", Modes: "dual"),
        new("designDirection", "Metric arrangement", "horizontal", "style", Choices: "horizontal:Horizontal|vertical:Vertical", Modes: "dual"),
        new("designAlign", "Text alignment", "center", "style", Choices: "start:Start|center:Center|end:End"),
        new("designBackground", "Background colour", "#13263a", "style"),
        new("designTitleColor", "Title color", "#8eabbf", "style"),
        new("designTextColor", "Value color", "#ffffff", "style"),
        new("designTitleSize", "Title size (% of basis)", 12d, "style", 4, 35),
        new("designTextSize", "Value size (% of basis)", 30d, "style", 6, 60),
        new("designPadding", "Padding (% of basis)", 8d, "style", 0, 20),
        new("designGap", "Gap (% of basis)", 5d, "style", 0, 15),
        new("designBarColor", "Bar color", "#54dfcc", "bar", Modes: "gauge"),
        new("designBarMin", "Bar minimum", 0d, "bar", -1e12, 1e12, Modes: "gauge"),
        new("designBarMax", "Bar maximum", 100d, "bar", -1e12, 1e12, Modes: "gauge"),
        new("designBarThickness", "Bar thickness (% of basis)", 6d, "bar", 1, 30, Modes: "gauge")
    ];
    private readonly Dictionary<string, JsonElement> values;
    public DesignSettings(JsonElement data)
    {
        values = Fields.ToDictionary(f => f.Key, f => data.ValueKind == JsonValueKind.Object && data.TryGetProperty(f.Key, out var v)
            ? v.Clone() : JsonSerializer.SerializeToElement(f.Key == "designPreset" ? "xml" : f.Default));
    }
    public JsonElement Get(string key) => values[key];
    public string String(string key) => Get(key).ValueKind == JsonValueKind.String ? Get(key).GetString()! : throw new FormatException($"{key} must be a string.");
    public string Preset => String("designPreset");
    public static void AddDefaults(JsonObject data)
    {
        foreach (var field in Fields) data[field.Key] = JsonSerializer.SerializeToNode(field.Default);
    }
    public static void AddSchema(JsonObject properties)
    {
        foreach (var f in Fields)
        {
            JsonObject schema = f.Default is double
                ? new() { ["type"] = "number", ["minimum"] = f.Min, ["maximum"] = f.Max }
                : new() { ["type"] = "string", ["maxLength"] = 512 };
            if (f.Choices != null) schema["enum"] = JsonSerializer.SerializeToNode(f.Choices.Split('|').Select(c => c.Split(':')[0]));
            properties[f.Key] = schema;
        }
    }
    private double Number(string key)
    {
        var field = Fields.Single(f => f.Key == key);
        if (Get(key).ValueKind != JsonValueKind.Number || !Get(key).TryGetDouble(out var number) || !double.IsFinite(number) || number < field.Min || number > field.Max)
            throw new FormatException($"{field.Label} must be between {field.Min} and {field.Max}.");
        return number;
    }
    private string Color(string key)
    {
        var color = String(key);
        if (!Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$")) throw new FormatException("Use a #rrggbb color.");
        return color;
    }
    private string Choice(string key)
    {
        var value = String(key);
        if (!Fields.Single(f => f.Key == key).Choices!.Split('|').Any(c => c.Split(':')[0] == value)) throw new FormatException("Unsupported display setting: " + key);
        return value;
    }
    public UiElement Render(IReadOnlyDictionary<string, JsonElement> data)
    {
        var preset = Choice("designPreset");
        if (preset == "xml") throw new FormatException("Render XML using the detailed editor.");
        string Value(string keyField, string fallbackField)
        {
            var key = String(keyField); DataHub.ValidateName(key);
            return data.TryGetValue(key, out var value)
                ? value.ValueKind == JsonValueKind.Null ? "—" : value.ValueKind == JsonValueKind.String ? value.GetString()! : value.ToString()
                : String(fallbackField);
        }
        UiTextRun Text(string id, string text, bool title) => new()
        {
            Key = id, Text = text, Color = Color(title ? "designTitleColor" : "designTextColor"), Align = Choice("designAlign"),
            Size = Number(title ? "designTitleSize" : "designTextSize") / 100, MinSize = .04,
            Weight = title ? "regular" : "bold", Wrap = false, MaxLines = 1
        };
        var primary = Value("designKey", "designFallback");
        var children = new List<UiElement>();
        if (preset == "dual")
        {
            UiStack Metric(string id, string title, string value) => new() { Key = id, Fill = true, Direction = "vertical", Justify = "center", Align = "stretch", Gap = Number("designGap") / 100,
                Children = [Text("title", title, true), Text("value", value, false)] };
            children.Add(new UiStack { Key = "metrics", Fill = true, Direction = Choice("designDirection"), Align = "stretch", Gap = Number("designGap") / 100,
                Children = [Metric("first", String("designTitle"), primary + String("designUnit")),
                    Metric("second", String("designSecondTitle"), Value("designSecondKey", "designSecondFallback") + String("designSecondUnit"))] });
        }
        else
        {
            if (String("designTitle").Length > 0) children.Add(Text("title", String("designTitle"), true));
            children.Add(Text("value", primary + String("designUnit"), false));
            if (preset == "gauge")
            {
                var min = Number("designBarMin"); var max = Number("designBarMax");
                if (max <= min) throw new FormatException("Bar maximum must be greater than minimum.");
                if (!double.TryParse(primary, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
                    throw new FormatException("Value and bar requires numeric data. Use Large text for string values.");
                children.Add(new UiRangeBar { Key = "level", Start = 0, End = Math.Clamp((number - min) / (max - min), 0, 1),
                    StartColor = Color("designBarColor"), EndColor = Color("designBarColor"), Thickness = Number("designBarThickness") / 100 });
            }
        }
        return new UiStack { Key = "content", Fill = true, Direction = "vertical", Justify = "center", Align = "stretch",
            Background = Color("designBackground"), Padding = Number("designPadding") / 100, Gap = Number("designGap") / 100, Children = children };
    }
}

