namespace Ziopuzzle.CustomButton;

/// <summary>Canonical XML values shared by rendering validation and editor suggestions.</summary>
public static class LayoutOptions
{
    private static readonly string[] TransitionCandidates = "opacity color endColor background x y x1 y1 x2 y2 width height length thickness radius cx cy angle startAngle sweepAngle value zoom offsetX offsetY gradientAngle gradientX gradientY".Split(' ');
    public static string[] TransitionProperties(string component) => TransitionCandidates
        .Concat(new[] { "rotation", "originX", "originY", "endAngle" })
        .Where(p => LayoutRenderer.Allowed(component).Split(' ').Contains(p) && (p != "value" || component is "bar" or "gauge")).ToArray();
    public static string[] Choices(string component, string attribute) => attribute switch
    {
        "name" when component == "icon" => IconNames,
        "coordinates" => ["widget", "local"],
        "gradient" => ["none", "linear", "radial"],
        "fit" => ["contain", "cover"],
        "zoom" => ["0.5", "1", "1.25", "1.5", "2", "4"],
        "offsetX" or "offsetY" => ["-1", "-0.5", "0", "0.5", "1"],
        "transition" => ["none", "crossfade"],
        "easing" => ["linear", "ease-in", "ease-out", "ease-in-out"],
        "colorSpace" => ["linear-rgb", "srgb", "hsv"],
        "transitionMs" => ["0", "200", "400", "1000"],
        "transitionProperties" => TransitionProperties(component),
        "direction" => ["vertical", "horizontal"],
        "corner" => ["square", "rounded"],
        "align" when component == "stack" => ["start", "center", "end", "stretch", "baseline"],
        "align" => ["start", "center", "end"],
        "justify" => ["start", "center", "end", "space-between"],
        "weight" => ["regular", "medium", "semibold", "bold"],
        "role" => ["primary", "secondary", "muted"],
        "borderStyle" => ["none", "static", "heartbeat", "breathing", "blink", "comet", "ants", "hue-shift", "rgb"],
        "fill" or "visible" or "wrap" or "seconds" or "interactive" => ["true", "false"],
        "mainSize" => ["auto"],
        _ => []
    };
    // Keep the editor and validator aligned with the installed SDK's icon catalogue.
    private static readonly string[] IconNames = typeof(MacroDeck.Ui.Components.UiIcons).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string)).Select(f => (string)f.GetRawConstantValue()!).Order(StringComparer.Ordinal).ToArray();
}
