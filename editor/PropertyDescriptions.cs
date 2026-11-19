namespace Ziopuzzle.CustomButton;

// Field wording belongs with property metadata, independently of Avalonia control wiring.
internal static class PropertyDescriptions
{
    internal const string DefaultHelp = "Leave blank for the default. Use {{dataName}}, {{= used / total * 100:F1}}%, or {{= floor(position / 1000):duration}} for milliseconds. Math supports floor, ceil, round, abs, min, max, clamp and more.";
    private const string ExperimentalNotice = " Experimental: behavior and configuration may change in future versions.";

    internal static string? Help(string component, string attribute) => attribute switch
    {
        "id" => "Unique element ID. Valid edits apply automatically. Check the Actions tab for matching event references after changing IDs or layout structure.",
        "anchor" when component == "scroll" => "Vertical scroll only. end follows the bottom while pinned; manually scrolling away suspends following. start disables following; it is not a command to jump to the top. Requires List version 3. Supports bindings and styles.",
        "followKey" => "Experimental: display data name containing a zero-based Repeat index. On change, temporarily removes rows and follows the target plus offset. Requires one direct Repeat child. Overrides anchor while enabled; missing/negative values disable following." + ExperimentalNotice,
        "followOffset" => "Item offset from the target index. 0 aligns the target at the bottom; 1 includes the next item. This is not a pixel offset or guaranteed centering." + ExperimentalNotice,
        "followDelayMs" => "Experimental phase delay, 0–5000 ms (default 300). Zero adds no intentional wait, but updates still use the session refresh cadence and transport delivery. Short delays can miss scrolling; increase if needed." + ExperimentalNotice,
        "itemAlias" or "indexAlias" or "keyAlias" => "Literal local variable name; no braces or functions. Defaults: item, index, itemKey. Must be distinct. These read-only names shadow same-named Channel values only inside Repeat.",
        "items" => "Array data name, {{tracks}}, or an array expression such as {{= split(text, ',')}} / {{= slice(tracks, 0, 10)}}. Use the name menu or the function editor. Children render once per item; missing/null arrays render no children.",
        "itemKey" => "Optional literal property path such as id. Values must be unique, nonempty strings or numbers. Keeps item identity when reordered. Blank uses the zero-based index.",
        "color" when component is "image" or "svg" => "Optional single-colour tint. Preserves the image alpha mask and replaces all RGB colours. Leave blank for original colours. Supports bindings, conditions and colour transitions. Colour alpha multiplies opacity. Requires client mask support; tinted artwork does not crossfade on beta.15.",
        "rasterSize" => "SVG image's longest edge in pixels: 64–1024, default 512. The aspect ratio is preserved. SVG variables update with display data; unchanged SVG is cached.",
        "frame" => "square: use size for a square frame (default). container: fit the parent's allocated rectangle; size is ignored and fill defaults to true. Background stays transparent. In a scrolling layout, give the image a mainSize or an enclosing modifier a height.",
        "size" when component is "image" or "svg" => "Edge of the square image frame. Ignored when Image frame is container; use the parent's layout or mainSize instead.",
        "averageColorKey" => "Optional unique literal output name, read with {{name}}. Live widgets/modals also write it to Channel data for variable-write and copy actions. Preview edits stay local. Supports files, HTTP images, inline SVG and artwork, but not Icon Packs.",
        "averageColorFallback" => "Literal color used before a result is available; default transparent. Alpha is supported. Source-load failures retain the last successful image and color.",
        "transitionMs" => "0 disables transitions. Range: 0–10000 ms. Only later changes animate; the initial value appears immediately.",
        "angle" => "Degrees: 0 points right, 90 down. Use cx/cy/length; clear x1/y1/x2/y2 and x/y/direction. Animate angle to rotate a needle without shortening it. Use a square box for a circular sweep.",
        "offsetX" or "offsetY" => "Use a fraction from -1 to 1: 0.2 moves by 20% of the image box. Positive X moves right; positive Y moves down. The offset is applied after zoom. Supports data bindings and transitions.",
        "zoom" => "Multiplier after contain/cover fitting. 1 is normal; 1.5 is 150%. Range: 0.1–4. Supports data bindings and transitions.",
        "fontFace" => "Macro Deck font catalogue ID, not a Windows font name or file path. Use {{fontId}} for display data. Leave blank for the default font; unavailable IDs fall back to the client's default. Copy the ID from a standard button's saved font settings.",
        "source" => "An absolute file path, HTTP/HTTPS URL, Macro Deck /api/... URL, artwork:/api/music-player/artwork/..., or icon-pack:UUID. Copy the UUID from icon.reference in a standard button's saved JSON. Use {{imageUrl}} for changing artwork. PNG, JPEG, WebP and GIF, up to 2 MiB. Blank hides the image.",
        "transitionProperties" => "Separate names with spaces, e.g. opacity color. Default: opacity. Only properties supported by this component are accepted.",
        _ => null
    };

    internal static string Label(string attribute) => attribute switch
    {
        "id" => "Element ID",
        "followKey" => "Follow data key (experimental)",
        "followOffset" => "Follow item offset (experimental)",
        "followDelayMs" => "Follow delay (ms) (experimental)",
        "closeModal" => "Close modal on press (no press event)",
        "result" => "Modal result (requires Close modal)",
        "start" => "Range start (same units as value)", "marker" => "Marker (same units as value)",
        "items" => "Array source", "itemKey" => "Item key property", "itemAlias" => "Item variable name", "indexAlias" => "Index variable name", "keyAlias" => "Item key variable name",
        "format" => "Time / date format", "zone" => "Time zone (IANA name)", "clip" => "Clip shape", "disabled" => "Disable child interactions",
        "minWidth" => "Minimum width (variant: cells; modifier: length)", "maxWidth" => "Maximum width (variant: cells; modifier: length)",
        "minHeight" => "Minimum height (variant: cells; modifier: length)", "maxHeight" => "Maximum height (variant: cells; modifier: length)",
        "minAspect" => "Minimum width / height ratio", "maxAspect" => "Maximum width / height ratio",
        "name" => "Built-in icon name", "rotation" => "Rotation (clockwise degrees)",
        "originX" => "Pivot X (fraction of own width)", "originY" => "Pivot Y (fraction of own height)",
        "endAngle" => "End angle (degrees)",
        "angle" => "Line angle (degrees)",
        "fit" => "Image fit (contain / cover)",
        "frame" => "Image frame (square / container)",
        "zoom" => "Zoom (1 = normal)", "offsetX" => "Horizontal offset (fraction of width)", "offsetY" => "Vertical offset (fraction of height)",
        "source" => "Image source (file / URL / Icon Pack)", "averageColorKey" => "Average color output name", "averageColorFallback" => "Average color fallback", "transition" => "Image change transition", "brightness" => "Brightness (0–2)", "saturation" => "Saturation (0–2)",
        "coordinates" => "Coordinates (widget basis / local box)",
        "fontFace" => "Font (Macro Deck catalogue ID)", "weight" => "Font weight (regular / medium / semibold / bold)",
        "x1" => "Start X (0–100%)", "y1" => "Start Y (0–100%)", "x2" => "End X (0–100%)", "y2" => "End Y (0–100%)",
        "gradient" => "Fill gradient", "gradientAngle" => "Gradient angle (0 up, 90 right)", "gradientX" => "Radial center X (0–100%)", "gradientY" => "Radial center Y (0–100%)",
        "transitionMs" => "Transition duration (ms)", "transitionProperties" => "Transition properties (space separated)", "easing" => "Transition easing", "colorSpace" => "Color interpolation space",
        "keyX" => "X data key", "keyY" => "Y data key", "leftValue" => "Left edge value", "rightValue" => "Right edge value", "topValue" => "Top edge value", "bottomValue" => "Bottom edge value", "stepX" => "X step (0 = continuous)", "stepY" => "Y step (0 = continuous)",
        "showCursorWhileTouching" => "Show cursor only while touching", "interactive" => "Enable interaction", "step" => "Value step", "key" => "Display data key",
        "shadow" => "Text shadow", "trackColor" => "Track colour", "opacity" => "Opacity (0–1)", "strokeColor" => "Stroke color", "strokeWidth" => "Stroke width", "cornerRadius" => "Corner radius", "data" => "Path (absolute coordinates)",
        "cx" => "Center X", "cy" => "Center Y", "radius" => "Radius", "startAngle" => "Start angle (degrees)", "sweepAngle" => "Sweep angle (degrees)", "points" => "Point count / polygon coordinates (x,y; x,y; …)",
        "x" => "Position from left", "y" => "Position from top", "width" => "Width", "height" => "Height", "length" => "Line length", "corner" => "Corners (square / rounded)",
        "direction" => "Direction", "color" => "Colour", "background" => "Background colour", "size" => "Size (e.g. 18%)", "mainSize" => "Main-axis size", "fill" => "Fill remaining space", "visible" => "Visible", "visibleWhen" => "Visibility condition", "padding" => "Padding", "gap" => "Gap between components", "align" => "Cross-axis alignment", "justify" => "Main-axis alignment", "borderStyle" => "Border style", "borderColor" => "Border colour", "value" => "Display value", "min" => "Minimum", "max" => "Maximum", "thickness" => "Thickness", "endColor" => "End colour", _ => attribute
    };
}
