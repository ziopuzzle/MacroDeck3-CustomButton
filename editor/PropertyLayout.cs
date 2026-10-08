namespace Ziopuzzle.CustomButton;

// Presentation order is independent of the renderer's XML validation whitelist.
internal static class PropertyLayout
{
    internal static readonly string[] Groups = ["Content and data", "X axis", "Y axis", "Layout and geometry", "Appearance", "Interaction", "Visibility", "Animation", "Advanced"];
    internal static string Group(string type, string name)
    {
        if (name == "id") return "Identity";
        if (name is "visible" or "visibleWhen") return "Visibility";
        if (name is "transitionMs" or "transitionProperties" or "easing" or "colorSpace" or "transition") return "Animation";
        if (name is "interactive" or "disabled" or "showCursorWhileTouching") return "Interaction";
        if (name == "rasterSize") return "Advanced";
        if (type == "trackpad")
        {
            if (name is "keyX" or "leftValue" or "rightValue" or "stepX") return "X axis";
            if (name is "keyY" or "topValue" or "bottomValue" or "stepY") return "Y axis";
        }
        if (name is "value" or "key" or "min" or "max" or "step" or "source" or "name" or "zone" or "format" or "seconds" or "positionMs" or "durationMs" or "anchor" or "rate" or "start" or "marker" or "data") return "Content and data";
        if (type == "chart" && name == "points") return "Content and data";
        if (name is "color" or "endColor" or "background" or "opacity" or "role" or "strokeColor" or "strokeWidth" or "borderStyle" or "borderColor" or "gradient" or "gradientAngle" or "gradientX" or "gradientY" or "brightness" or "saturation" or "thickness" or "plotTop") return "Appearance";
        if (name is "fontFace" or "weight" or "digits" or "sizeCap" or "minSize" || name == "size" && type is "text" or "dynamic-text") return "Appearance";
        return "Layout and geometry";
    }
    private static readonly string[] Order = ("id source name key value min max step start marker format zone seconds positionMs durationMs anchor rate points data " +
        "keyX leftValue rightValue stepX keyY topValue bottomValue stepY " +
        "coordinates direction x y x1 y1 x2 y2 cx cy length angle radius startAngle endAngle sweepAngle width height size " +
        "minWidth maxWidth minHeight maxHeight minAspect maxAspect fill mainSize justify align gap padding fit zoom offsetX offsetY rotation originX originY clip corner cornerRadius wrap maxLines " +
        "fontFace weight sizeCap minSize digits color role gradient endColor gradientAngle gradientX gradientY background brightness saturation thickness plotTop strokeColor strokeWidth borderStyle borderColor opacity " +
        "interactive disabled showCursorWhileTouching visible visibleWhen transition transitionMs transitionProperties easing colorSpace rasterSize").Split(' ', StringSplitOptions.RemoveEmptyEntries);
    internal static string[] Attributes(string type) => LayoutRenderer.Allowed(type).Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .OrderBy(name => name == "size" && type is "text" or "dynamic-text" ? Array.IndexOf(Order, "weight") + 0.5 : Array.IndexOf(Order, name) is var index && index >= 0 ? index : int.MaxValue).ToArray();
}
