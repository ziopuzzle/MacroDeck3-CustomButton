namespace Ziopuzzle.CustomButton;

public sealed record LayoutTemplate(string Id, string Name, string Xml, string InitialValues, string Category);

public static class LayoutTemplates
{
    private static string Resource(string name, bool optional = false)
    {
        using var stream = typeof(LayoutTemplates).Assembly.GetManifestResourceStream("Templates." + name);
        if (stream == null) return optional ? "{}" : throw new InvalidOperationException("Missing template: " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim();
    }
    private static string Title(string id) => id switch
    {
        "basic_animation" => "Animation", "basic_bar" => "Text and bar", "basic_border" => "Border",
        "basic_calculation" => "Calculated sector", "basic_conditional" => "Conditional styles",
        "basic_gradation" => "Gradients", "basic_progress" => "Playback progress", "basic_shape" => "Shapes",
        "basic_slider" => "Slider", "widget_clock" => "Clock", "widget_history-chart" => "History Graph",
        "basic_gauge" => "Arc gauge", "basic_icon" => "Built-in icons", "basic_image" => "Image sources", "basic_transform" => "Transform a group",
        "basic_trackpad" => "Trackpad", "basic_svg" => "Variable SVG", "basic_dial" => "Dial", "basic_toggle" => "Toggle", "basic_segmented" => "Segmented",
        "basic_range" => "Range and marker", "basic_dynamic-text" => "Client time and date",
        "basic_modifier" => "Modifier", "basic_responsive" => "Responsive layouts",
        "widget_meter" => "Meter", "widget_nowplaying_player" => "Now playing",
        _ => id[(id.IndexOf('_') + 1)..].Replace('_', ' ').Replace('-', ' ')
    };
    // Embedded examples are the single source of truth. Missing companion JSON means empty data.
    public static IReadOnlyList<LayoutTemplate> All { get; } = typeof(LayoutTemplates).Assembly.GetManifestResourceNames()
        .Where(n => n.StartsWith("Templates.", StringComparison.Ordinal) && n.EndsWith(".xml", StringComparison.Ordinal))
        .Select(n => n["Templates.".Length..^4])
        .OrderBy(id => id.StartsWith("basic_", StringComparison.Ordinal) ? 0 : 1).ThenBy(id => id, StringComparer.Ordinal)
        .Select(id => new LayoutTemplate(id, Title(id), Resource(id + ".xml"), Resource(id + ".json", optional: true),
            id.StartsWith("basic_", StringComparison.Ordinal) ? "BASIC" : "ADVANCED")).ToArray();
    public static LayoutTemplate Get(string id) => All.First(t => t.Id == id);
}
