using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Text.Json;
using MacroDeck.Ui.Components;
using MacroDeck.Ui.Dsl;
using MacroDeck.Ui.Model.References;
using MacroDeck.Ui.Model.Resources;

namespace Ziopuzzle.CustomButton;

public sealed class LayoutRenderer
{
    private static readonly Regex Slot = new(@"\{\{\s*([A-Za-z0-9_.-]+)\s*(?::([^{}]{1,32}))?\s*\}\}", RegexOptions.CultureInvariant);
    internal static string Allowed(string type) => "id fill mainSize visible visibleWhen opacity transitionMs transitionProperties easing colorSpace " + (type switch
    {
        "stack" => "direction justify align gap padding background borderStyle borderColor interactive",
        "layer" => "interactive background",
        "transform" => "rotation originX originY zoom offsetX offsetY",
        "modifier" => "padding clip radius width height minWidth maxWidth minHeight maxHeight disabled",
        "responsive" => "",
        "variant" => "minWidth maxWidth minHeight maxHeight minAspect maxAspect",
        "dynamic-text" => "zone format seconds size sizeCap minSize weight color role align",
        "icon" => "name size color role",
        "gauge" => "value min max startAngle endAngle color thickness",
        "image" => "source size fit zoom offsetX offsetY transition brightness saturation color",
        "svg" => "rasterSize size fit zoom offsetX offsetY transition brightness saturation color",
        "text" => "size sizeCap minSize weight fontFace color role digits align wrap maxLines",
        "bar" => "value start marker min max color endColor thickness",
        "chart" => "key min max points color plotTop thickness",
        "clock" => "zone seconds color",
        "progress-bar" => "positionMs durationMs anchor rate color endColor thickness",
        "slider" => "value key min max step direction color thickness interactive",
        "rect" => "coordinates x y width height color corner cornerRadius strokeColor strokeWidth gradient endColor gradientAngle gradientX gradientY",
        "circle" or "capsule" => "coordinates x y width height color strokeColor strokeWidth gradient endColor gradientAngle gradientX gradientY",
        "path" => "coordinates x y width height data color strokeColor strokeWidth",
        "line" => "coordinates x y length thickness direction color x1 y1 x2 y2 cx cy angle gradient endColor gradientAngle gradientX gradientY",
        "polygon" => "coordinates points color strokeColor strokeWidth",
        "sector" => "coordinates cx cy radius startAngle sweepAngle color strokeColor strokeWidth",
        _ => throw new FormatException($"Unsupported component: {type}")
    });
    private readonly XElement root;
    public bool HasRootBackground(IReadOnlyDictionary<string, JsonElement> values)
        => root.Attribute("background") != null || root.Elements("style").Any(style =>
            style.Attribute("background") != null && DisplayCondition.Evaluate((string)style.Attribute("when")!, values));
    public LayoutRenderer(string layout)
    {
        if (layout.Length > LayoutLimits.XmlCharacters) throw new FormatException($"Layouts must not exceed {LayoutLimits.XmlCharacters} characters.");
        using var reader = XmlReader.Create(new StringReader(layout), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = LayoutLimits.XmlCharacters });
        root = XElement.Load(reader);
        var nodes = root.DescendantsAndSelf().ToArray();
        if (nodes.Length > LayoutLimits.XmlElements) throw new FormatException($"Layouts must not exceed {LayoutLimits.XmlElements} XML elements, including styles.");
        if (nodes.Any(e => e.Ancestors().Count() >= LayoutLimits.XmlDepth)) throw new FormatException($"Layouts must not exceed {LayoutLimits.XmlDepth} XML levels, including the root.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            if (node.Name.Namespace != XNamespace.None) throw new FormatException("XML namespaces are not supported.");
            if (node.Name.LocalName == "style")
            {
                if (node.Parent == null || node.Parent.Name.LocalName == "style" || node.HasElements || !string.IsNullOrWhiteSpace(node.Value)) throw new FormatException("Place style directly inside a component and leave it empty.");
                var condition = (string?)node.Attribute("when") ?? throw new FormatException("A style needs a when condition.");
                DisplayCondition.Evaluate(condition, new Dictionary<string, JsonElement>());
                foreach (var attribute in node.Attributes())
                    if (attribute.Name != "when" && (attribute.Name == "id" || !Allowed(node.Parent.Name.LocalName).Split(' ').Contains(attribute.Name.ToString()))) throw new FormatException("Unsupported style attribute: " + attribute.Name);
                continue;
            }
            var id = (string?)node.Attribute("id") ?? throw new FormatException("Each component needs an id.");
            if (!Regex.IsMatch(id, "^[A-Za-z][A-Za-z0-9_-]{0,31}$") || !ids.Add(id)) throw new FormatException("IDs must begin with a letter, contain at most 32 characters and be unique.");
            var allowed = Allowed(node.Name.LocalName);
            foreach (var attribute in node.Attributes())
                if (!allowed.Split(' ').Contains(attribute.Name.ToString())) throw new FormatException($"Unsupported attribute on {node.Name}: {attribute.Name}");
            if (node.Attribute("visibleWhen") is { } conditionAttribute) DisplayCondition.Evaluate(conditionAttribute.Value, new Dictionary<string, JsonElement>());
            if (!LayoutDocument.IsContainer(node) && node.Elements().Any(e => e.Name != "style")) throw new FormatException($"{node.Name} cannot contain child components.");
            if (node.Name == "responsive" && node.Elements().Any(e => e.Name != "variant" && e.Name != "style")) throw new FormatException("Responsive children must be variant components; the first is the default layout.");
            if (node.Name == "variant" && node.Parent?.Name != "responsive") throw new FormatException("Place variant directly inside responsive.");
            if (node.Name.LocalName is not ("text" or "svg") && node.Nodes().OfType<XText>().Any(t => !string.IsNullOrWhiteSpace(t.Value))) throw new FormatException("Place text inside a text or SVG component.");
        }
    }
    public static string Expand(string text, IReadOnlyDictionary<string, JsonElement> values)
        => Slot.Replace(Regex.Replace(text, @"\{\{\s*=([^{}]*)\}\}", m =>
        {
            var expression = m.Groups[1].Value;
            var separator = expression.IndexOf(':');
            var result = DisplayMath.Evaluate(separator < 0 ? expression : expression[..separator], values);
            return separator < 0 ? result : DisplayFormat.Apply(result, expression[(separator + 1)..]);
        }), match =>
        {
            if (!values.TryGetValue(match.Groups[1].Value, out var value) || value.ValueKind == JsonValueKind.Null) return "—";
            var raw = value.ValueKind == JsonValueKind.String ? value.GetString()! : value.ToString();
            if (match.Groups[2].Success)
            {
                return DisplayFormat.Apply(raw, match.Groups[2].Value);
            }
            return raw;
        });

    public UiElement Render(IReadOnlyDictionary<string, JsonElement> values, Func<string, int, IReadOnlyList<double>>? history = null, string? selectedId = null, Action<ControlInput>? input = null, DisplayAnimation? animation = null, Func<string, string, UiResource?>? images = null, Func<string, double>? imageAspectRatio = null)
    {
        animation?.BeginFrame();
        try { return Make(root, values, history, false, selectedId, input, animation, images, imageAspectRatio) ?? new UiStack { Key = "hidden", Fill = true, Children = [] }; }
        finally { animation?.EndFrame(); }
    }
    private static UiElement? Make(XElement node, IReadOnlyDictionary<string, JsonElement> values, Func<string, int, IReadOnlyList<double>>? history, bool parentHorizontal, string? selectedId, Action<ControlInput>? input, DisplayAnimation? animation, Func<string, string, UiResource?>? images, Func<string, double>? imageAspectRatio)
    {
        var element = MakeCore(node, values, history, parentHorizontal, selectedId, input, animation, images, imageAspectRatio);
        return element != null && (string?)node.Attribute("id") == selectedId ? PreviewSelection.Highlight(element) : element;
    }
    private static UiElement? MakeCore(XElement node, IReadOnlyDictionary<string, JsonElement> values, Func<string, int, IReadOnlyList<double>>? history, bool parentHorizontal, string? selectedId, Action<ControlInput>? input, DisplayAnimation? animation, Func<string, string, UiResource?>? images, Func<string, double>? imageAspectRatio)
    {
        var attributes = node.Attributes().ToDictionary(a => a.Name.LocalName, a => a.Value);
        foreach (var style in node.Elements("style"))
            if (DisplayCondition.Evaluate((string)style.Attribute("when")!, values))
                foreach (var attribute in style.Attributes().Where(a => a.Name != "when")) attributes[attribute.Name.LocalName] = attribute.Value;
        string A(string name, string fallback = "") => Expand(attributes.GetValueOrDefault(name, fallback), values).Trim();
        var durationText = A("transitionMs", "0");
        if (!double.TryParse(durationText, NumberStyles.Float, CultureInfo.InvariantCulture, out var duration) || !double.IsFinite(duration) || duration < 0 || duration > 10000)
            throw new FormatException("transitionMs must be between 0 and 10000 milliseconds.");
        var easing = A("easing", "ease-out");
        var colorSpace = A("colorSpace", "linear-rgb");
        if (!LayoutOptions.Choices(node.Name.LocalName, "colorSpace").Contains(colorSpace)) throw new FormatException("Use linear-rgb, srgb or hsv for colorSpace.");
        if (!LayoutOptions.Choices(node.Name.LocalName, "easing").Contains(easing)) throw new FormatException("Use linear, ease-in, ease-out or ease-in-out easing.");
        var animated = A("transitionProperties", "opacity").Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var supported = LayoutOptions.TransitionProperties(node.Name.LocalName);
        foreach (var property in animated)
            if (!supported.Contains(property))
                throw new FormatException($"Transition property {property} is not supported on {node.Name}.");
        double Animate(string name, double value) => duration > 0 && animated.Contains(name) && animation != null
            ? animation.Value((string)node.Attribute("id")! + "." + name, value, duration, easing) : value;
        double N(string name, double fallback, double min = 0, double max = 1)
        {
            var raw = A(name);
            if (raw is "" or "—") return Animate(name, fallback);
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || !double.IsFinite(n) || n < min || n > max)
                throw new FormatException($"{node.Attribute("id")?.Value}: {name} must be a number between {min} and {max}.");
            return Animate(name, n);
        }
        bool B(string name, bool fallback)
        {
            var raw = A(name);
            if (raw is "" or "—") return fallback;
            if (bool.TryParse(raw, out var b)) return b;
            if (raw is "1" or "0") return raw == "1";
            throw new FormatException($"{name} must be true or false.");
        }
        if (!B("visible", true) || attributes.TryGetValue("visibleWhen", out var visibleWhen) && !DisplayCondition.Evaluate(visibleWhen, values)) return null;
        double Length(string name, double fallback, double max = 4)
        {
            var raw = A(name);
            if (raw is "" or "auto" or "—" or "—%") return Animate(name, fallback);
            bool percent = raw.EndsWith('%');
            if (percent) raw = raw[..^1];
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || !double.IsFinite(n)) throw new FormatException($"Specify {name} as 0.2 or 20%.");
            n = percent ? n / 100 : n;
            if (n < 0 || n > max) throw new FormatException($"{name} must be between 0 and {max}.");
            return Animate(name, n);
        }
        int Integer(string name, int fallback, int min, int max)
        {
            var n = N(name, fallback, min, max);
            return n == Math.Truncate(n) ? (int)n : throw new FormatException($"{name} must be an integer.");
        }
        string Choice(string name, string fallback)
        {
            var options = LayoutOptions.Choices(node.Name.LocalName, name);
            var value = A(name, fallback).ToLowerInvariant();
            value = value switch { "row" when name == "direction" => "horizontal", "column" when name == "direction" => "vertical", "left" or "top" when name is "align" or "justify" => "start", "right" or "bottom" when name is "align" or "justify" => "end", _ => value };
            return options.Contains(value) ? value : throw new FormatException($"{name} must be one of {string.Join('/', options)}.");
        }
        var colors = new Dictionary<string, DisplayColor>();
        string Color(string name, string fallback)
        {
            var color = DisplayColor.Parse(A(name, fallback));
            if (duration > 0 && animated.Contains(name) && animation != null)
            {
                // Keep alpha/structure constant during RGB interpolation. Use opacity for fades.
                var rgb = animation.Color((string)node.Attribute("id")! + "." + name, color.Rgb, duration, easing, colorSpace);
                color = new DisplayColor(rgb, color.Alpha);
            }
            colors[name] = color;
            return color.Rgb;
        }
        string EndColor() => Color("endColor", colors["color"].Hex);
        var key = (string)node.Attribute("id")!;
        var fill = B("fill", node.Parent == null || parentHorizontal || node.Name.LocalName is "layer" or "chart" or "clock" or "slider" or "gauge" or "transform" or "modifier" or "responsive");
        // Keep omitted sizes unset; a numeric ternary would turn default into an explicit zero.
        UiSize mainSize = default;
        if (attributes.ContainsKey("mainSize") && A("mainSize") != "auto") mainSize = Length("mainSize", 0);
        var horizontal = node.Name == "stack" && Choice("direction", "vertical") == "horizontal";
        var children = node.Elements().Where(e => e.Name != "style").Select(n => Make(n, values, history, horizontal, selectedId, input, animation, images, imageAspectRatio)
            ?? (node.Name == "responsive" ? new UiLayer { Key = (string)n.Attribute("id")!, Children = [] } : null)).OfType<UiElement>().ToArray();
        var textSize = Length("size", .18, 1);
        var minTextSize = Math.Min(Length("minSize", Math.Min(.08, textSize), 1), textSize);
        UiSize TextLength(double size) => attributes.ContainsKey("sizeCap") ? UiSize.Capped(size, N("sizeCap", 32, 1, 256)) : (UiSize)size;
        UiProgressReference Progress()
        {
            long Milliseconds(string name) {
                var value = N(name, 0, 0, 1e12);
                return value == Math.Truncate(value) ? (long)value : throw new FormatException($"{name} must be an integer number of milliseconds.");
            }
            var rate = N("rate", 0, -16, 16);
            var anchorText = A("anchor");
            var anchor = DateTimeOffset.UnixEpoch;
            if (anchorText is not ("" or "—"))
            {
                if (!Regex.IsMatch(anchorText, @"^\d{4}-\d{2}-\d{2}T.*(?:Z|[+-]\d{2}:\d{2})$") ||
                    !DateTimeOffset.TryParse(anchorText, CultureInfo.InvariantCulture, DateTimeStyles.None, out anchor))
                    throw new FormatException("anchor must be an ISO timestamp with a time zone, e.g. 2026-09-13T12:00:00+09:00.");
            }
            else if (rate != 0) throw new FormatException("When rate is nonzero, set anchor to the time positionMs was measured.");
            // A stable anchor avoids resetting playback on each refresh or unrelated data update.
            return new UiProgressReference { PositionMs = Milliseconds("positionMs"),
                DurationMs = A("durationMs") is "" or "—" ? null : Milliseconds("durationMs"), Anchor = anchor, Rate = rate };
        }
        UiElement? Build()
        {
            switch (node.Name.LocalName)
            {
                case "responsive":
                    var branches = node.Elements("variant").ToArray();
                    var variants = new List<UiResponsiveVariant>();
                    for (var i = 1; i < branches.Length; i++)
                    {
                        var branch = branches[i];
                        var settings = branch.Attributes().ToDictionary(a => a.Name.LocalName, a => a.Value);
                        foreach (var style in branch.Elements("style"))
                            if (DisplayCondition.Evaluate((string)style.Attribute("when")!, values))
                                foreach (var attribute in style.Attributes().Where(a => a.Name != "when")) settings[attribute.Name.LocalName] = attribute.Value;
                        double? Bound(string name)
                        {
                            if (!settings.TryGetValue(name, out var raw)) return null;
                            var text = Expand(raw, values).Trim();
                            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value)
                                || value < 0 || (name.EndsWith("Aspect") && value == 0)) throw new FormatException($"{name} needs a nonnegative cell count or positive aspect ratio.");
                            return value;
                        }
                        var minW = Bound("minWidth"); var maxW = Bound("maxWidth"); var minH = Bound("minHeight"); var maxH = Bound("maxHeight");
                        var minA = Bound("minAspect"); var maxA = Bound("maxAspect");
                        if (minW >= maxW || minH >= maxH || minA >= maxA) throw new FormatException("Responsive maximum bounds must exceed minimum bounds.");
                        variants.Add(new UiResponsiveVariant { Content = new UiLayer { Key = "branch" + i, Children = [children[i]] },
                            MinWidth = minW, MaxWidth = maxW, MinHeight = minH, MaxHeight = maxH, MinAspect = minA, MaxAspect = maxA });
                    }
                    return new UiResponsive { Key = key, Fill = fill, MainSize = mainSize,
                        Default = new UiLayer { Key = "default", Children = children.Take(1).ToArray() }, Variants = variants,
                        Fallback = new UiTextRun { Key = "unsupported", Text = "Responsive layout requires a compatible client." } };
                case "variant":
                    return AlphaPaint.Layer(new UiLayer { Key = key, Fill = fill, MainSize = mainSize, Children = children });
                case "modifier":
                    UiLength? FrameLength(string name) => attributes.ContainsKey(name) ? UiLength.OfBasis(Length(name, 0)) : null;
                    var frame = new UiFrame { Width = FrameLength("width"), Height = FrameLength("height"),
                        MinWidth = FrameLength("minWidth"), MaxWidth = FrameLength("maxWidth"), MinHeight = FrameLength("minHeight"), MaxHeight = FrameLength("maxHeight") };
                    if (attributes.ContainsKey("minWidth") && attributes.ContainsKey("maxWidth") && Length("minWidth", 0) > Length("maxWidth", 0)
                        || attributes.ContainsKey("minHeight") && attributes.ContainsKey("maxHeight") && Length("minHeight", 0) > Length("maxHeight", 0))
                        throw new FormatException("Modifier maximum sizes must not be smaller than minimum sizes.");
                    return new UiModifier { Key = key, Fill = fill, MainSize = mainSize, Padding = Length("padding", 0),
                        Opacity = N("opacity", 1), Radius = Length("radius", 0), Disabled = B("disabled", false),
                        Clip = Choice("clip", "none") == "none" ? default : UiValue.Of(Choice("clip", "none")), Frame = frame,
                        Child = AlphaPaint.Layer(new UiLayer { Key = "content", Children = children }) };
                case "dynamic-text":
                    return new UiDynamicText { Key = key, Fill = fill, MainSize = mainSize,
                        Value = UiTimeReference.InZone(A("zone") is "—" ? null : A("zone")), Format = Choice("format", "time"), Seconds = B("seconds", false),
                        Size = TextLength(textSize), MinSize = TextLength(minTextSize), Weight = Choice("weight", "regular"), Align = Choice("align", "start"),
                        Role = Choice("role", "primary"), Color = attributes.ContainsKey("color") ? UiValue.Of(Color("color", "#ffffff")) : default };
                case "transform":
                    return new UiTransform { Key = key, Fill = fill, MainSize = mainSize, Children = children,
                        Rotation = N("rotation", 0, -1e12, 1e12), OriginX = N("originX", .5, -1e12, 1e12), OriginY = N("originY", .5, -1e12, 1e12),
                        Zoom = N("zoom", 1, 0.001, 1e12), OffsetX = N("offsetX", 0, -1e12, 1e12), OffsetY = N("offsetY", 0, -1e12, 1e12) };
                case "icon":
                    var iconSize = Length("size", .2);
                    var icon = new UiIcon { Key = key, Fill = B("fill", false), MainSize = mainSize, Icon = Choice("name", "star"),
                        Size = iconSize,
                        Role = Choice("role", "primary"), Color = attributes.ContainsKey("color") ? UiValue.Of(Color("color", "#ffffff")) : default };
                    // The host centres glyphs in their allocated box. Give a natural-size icon a
                    // square box so cross-axis stretching cannot introduce invisible vertical space.
                    if (B("fill", false) || attributes.ContainsKey("mainSize") && A("mainSize") != "auto") return icon;
                    return new UiModifier { Key = key, Fill = false,
                        Frame = new UiFrame { Width = UiLength.OfBasis(iconSize), Height = UiLength.OfBasis(iconSize) },
                        Child = icon with { Key = "glyph", Fill = default, MainSize = default } };
                case "gauge":
                    var gaugeMin = N("min", 0, -1e12, 1e12); var gaugeMax = N("max", 100, -1e12, 1e12);
                    if (gaugeMax <= gaugeMin) throw new FormatException("For gauge, max must be greater than min.");
                    return new UiGauge { Key = key, Fill = fill, MainSize = mainSize,
                        Level = Math.Clamp((N("value", gaugeMin, -1e12, 1e12) - gaugeMin) / (gaugeMax - gaugeMin), 0, 1),
                        StartAngle = N("startAngle", -135, -3600, 3600), EndAngle = N("endAngle", 135, -3600, 3600),
                        LevelColor = Color("color", "#54dfcc"), Thickness = Length("thickness", .04, 1) };
                case "image":
                case "svg":
                    var source = node.Name.LocalName == "svg"
                        ? SvgTemplate.Source(string.Concat(node.Nodes().OfType<XText>().Select(t => t.Value)), value => Expand(value, values), Integer("rasterSize", 512, 64, 1024))
                        : A("source");
                    var resource = images?.Invoke(key, source);
                    var imageSize = Length("size", 1, 1);
                    var imageFill = B("fill", false);
                    var imageOpacity = N("opacity", 1);
                    // Keep the native tint-capable tree stable when a conditional colour is cleared.
                    var tintMode = attributes.ContainsKey("color") || node.Elements("style").Any(s => s.Attribute("color") != null)
                        || duration > 0 && animated.Contains("color");
                    var tint = A("color") is "" or "—" ? null : Color("color", "#ffffff");
                    var tintAlpha = tint == null ? 1 : colors["color"].Opacity;
                    var image = new UiImage { Key = key, Fill = imageFill, MainSize = mainSize, Size = imageSize,
                        Source = resource == null ? default : UiValue.Of(resource), Transition = Choice("transition", "none") == "none" ? default : UiValue.Of("crossfade"),
                        Opacity = imageOpacity, Brightness = N("brightness", 1, 0, 2), Saturation = N("saturation", 1, 0, 2) };
                    var fit = Choice("fit", "contain");
                    var zoom = N("zoom", 1, .1, 4);
                    var offsetX = N("offsetX", 0, -1, 1);
                    var offsetY = N("offsetY", 0, -1, 1);
                    // Explicit framing keeps a stable tree while data-bound values change.
                    if (!tintMode && fit == "contain" && !attributes.ContainsKey("zoom") && !attributes.ContainsKey("offsetX") && !attributes.ContainsKey("offsetY")
                        && !(duration > 0 && animated.Overlaps(new[] { "zoom", "offsetX", "offsetY" })))
                    {
                        if (imageFill || attributes.ContainsKey("mainSize") && A("mainSize") != "auto") return image;
                        // ui.image centres artwork within the entire allocated cross axis. Give it
                        // a natural square footprint so its parent's start/center/end alignment wins.
                        return new UiModifier { Key = key, Fill = false,
                            Frame = new UiFrame { Width = UiLength.OfBasis(imageSize), Height = UiLength.OfBasis(imageSize) },
                            Child = image with { Key = "art", Fill = default, MainSize = default } };
                    }
                    var aspect = imageAspectRatio?.Invoke(key) ?? 1;
                    if (double.IsNaN(aspect) || tintMode)
                        return new UiModifier { Key = key, Fill = imageFill, MainSize = mainSize, Clip = "bounds", Radius = 0,
                            Frame = new UiFrame { Width = UiLength.OfBasis(imageSize), Height = UiLength.OfBasis(imageSize) },
                            Child = new UiButton { Key = "art", Source = image.Source, Transition = image.Transition,
                                Background = "transparent", Fit = fit, Zoom = zoom, OffsetX = offsetX, OffsetY = offsetY,
                                Tint = tint == null ? default : UiValue.Of(tint),
                                Opacity = imageOpacity * tintAlpha, Brightness = image.Brightness, Saturation = image.Saturation,
                                Children = [] } };
                    if (!double.IsFinite(aspect) || aspect <= 0) aspect = 1;
                    return new UiModifier { Key = key, Fill = imageFill, MainSize = mainSize, Clip = "bounds",
                        Frame = new UiFrame { Width = UiLength.OfBasis(imageSize), Height = UiLength.OfBasis(imageSize) },
                        Child = new UiTransform { Key = "crop", Zoom = zoom * (fit == "cover" ? Math.Max(aspect, 1 / aspect) : 1), OffsetX = offsetX, OffsetY = offsetY,
                            Children = [image with { Key = "art", Fill = true, MainSize = default }] } };
                case "polygon":
                case "sector":
                    if (node.Name.LocalName == "sector" && (Length("radius", .4, 1) == 0 || N("sweepAngle", 120, -360, 360) == 0)) return null;
                    var path = node.Name.LocalName == "polygon" ? ShapePaths.Polygon(A("points", "10%,90%;50%,10%;90%,90%"))
                        : ShapePaths.Sector(Length("cx", .5, 1), Length("cy", .5, 1), Length("radius", .4, 1), N("startAngle", -90, -360, 360), N("sweepAngle", 120, -360, 360));
                    var localCanvas = Choice("coordinates", "widget") == "local";
                    return new UiShape { Key = key, Shape = "path", Path = path, Color = Color("color", "#54dfcc"),
                        StrokeColor = attributes.ContainsKey("strokeColor") ? UiValue.Of(Color("strokeColor", "#ffffff")) : default,
                        StrokeWidth = Length("strokeWidth", .01), Fill = B("fill", localCanvas), MainSize = localCanvas || attributes.ContainsKey("mainSize") ? mainSize : (UiSize)1 };
                case "rect":
                case "circle":
                case "capsule":
                case "path":
                case "line":
                    var isLine = node.Name.LocalName == "line";
                    var localCoordinates = Choice("coordinates", "widget") == "local";
                    if (isLine && attributes.ContainsKey("angle"))
                    {
                        if (new[] { "x", "y", "direction", "x1", "y1", "x2", "y2" }.Any(attributes.ContainsKey))
                            throw new FormatException("Use cx/cy/length/angle instead of endpoints or x/y/direction for an angular line.");
                        var cx = Length("cx", .5, 1); var cy = Length("cy", .5, 1);
                        var length = Length("length", .4, 1);
                        var radians = N("angle", 0, -3600, 3600) * Math.PI / 180;
                        var thickness = Length("thickness", .02, 1);
                        if (length == 0 || thickness == 0) return null;
                        var lineColor = Color("color", "#54dfcc");
                        return AlphaPaint.Fade(new UiShape { Key = key, Shape = "path",
                            Path = FormattableString.Invariant($"M{cx} {cy} L{cx + length * Math.Cos(radians)} {cy + length * Math.Sin(radians)}"),
                            StrokeColor = lineColor, StrokeWidth = thickness, Fill = B("fill", localCoordinates),
                            MainSize = localCoordinates || attributes.ContainsKey("mainSize") ? mainSize : (UiSize)1 }, colors["color"].Opacity);
                    }
                    if (isLine && new[] { "x1", "y1", "x2", "y2" }.Any(attributes.ContainsKey))
                    {
                        if (!new[] { "x1", "y1", "x2", "y2" }.All(attributes.ContainsKey)) throw new FormatException("Specify all four line coordinates: x1, y1, x2, y2.");
                        if (new[] { "x", "y", "length", "direction" }.Any(attributes.ContainsKey)) throw new FormatException("Use either endpoints or x/y/length/direction for a line.");
                        var x1 = Length("x1", 0, 1); var y1 = Length("y1", 0, 1);
                        var x2 = Length("x2", 1, 1); var y2 = Length("y2", 1, 1);
                        var thickness = Length("thickness", .02, 1);
                        if ((x1 == x2 && y1 == y2) || thickness == 0) return null;
                        var lineColor = Color("color", "#54dfcc");
                        // Open paths use a stroke only; coordinates cover the available canvas.
                        return AlphaPaint.Fade(new UiShape { Key = key, Shape = "path",
                            Path = FormattableString.Invariant($"M{x1} {y1} L{x2} {y2}"), StrokeColor = lineColor,
                            StrokeWidth = thickness, Fill = B("fill", localCoordinates), MainSize = localCoordinates || attributes.ContainsKey("mainSize") ? mainSize : (UiSize)1 }, colors["color"].Opacity);
                    }
                    var verticalLine = isLine && Choice("direction", "horizontal") == "vertical";
                    var shapeWidth = isLine ? Length(verticalLine ? "thickness" : "length", verticalLine ? .02 : .8) : Length("width", .4);
                    var shapeHeight = isLine ? Length(verticalLine ? "length" : "thickness", verticalLine ? .8 : .02) : Length("height", node.Name.LocalName is "circle" or "path" ? .4 : .2);
                    var shapeX = Length("x", 0); var shapeY = Length("y", 0);
                    if (shapeWidth == 0 || shapeHeight == 0) return null;
                    var rounded = node.Name.LocalName == "rect" && Choice("corner", "square") == "rounded";
                    var shapeColor = Color("color", "#54dfcc");
                    if (localCoordinates)
                    {
                        if (node.Name.LocalName is "rect" or "circle" or "capsule" && Choice("gradient", "none") != "none")
                            throw new FormatException("Local coordinates currently support solid shapes. Use widget coordinates for gradient fills.");
                        var localPath = LocalShapePath.Build(node.Name.LocalName, shapeX, shapeY, shapeWidth, shapeHeight,
                            rounded ? Length("cornerRadius", .06) : 0, A("data", "M0 0 L1 0.5 L0 1 Z"));
                        return new UiShape { Key = key, Shape = "path", Path = localPath, Color = shapeColor,
                            Fill = B("fill", true), MainSize = mainSize,
                            StrokeColor = attributes.ContainsKey("strokeColor") ? UiValue.Of(Color("strokeColor", "#ffffff")) : default,
                            StrokeWidth = Length("strokeWidth", .01) };
                    }
                    UiElement body = AlphaPaint.Apply(new UiShape { Key = "body", MainSize = shapeWidth, Fill = false,
                        Shape = rounded ? "rounded-rectangle" : node.Name.LocalName is "circle" or "capsule" or "path" ? node.Name.LocalName : "rectangle",
                        CornerRadius = Length("cornerRadius", .06), Color = shapeColor,
                        Path = node.Name.LocalName == "path" ? UiValue.Of(ShapePaths.Validate(A("data", "M0 0 L1 0.5 L0 1 Z"))) : default,
                        StrokeColor = attributes.ContainsKey("strokeColor") ? UiValue.Of(Color("strokeColor", "#ffffff")) : default,
                        StrokeWidth = Length("strokeWidth", .01) }, colors);
                    if (node.Name.LocalName is "rect" or "circle" or "capsule" && Choice("gradient", "none") is var gradientKind && gradientKind != "none")
                    {
                        var endColor = Color("endColor", colors["color"].Hex);
                        if (colors["color"].Alpha != colors["endColor"].Alpha) throw new FormatException("Use the same alpha for both gradient colors. Use opacity to fade the entire shape.");
                        if (node.Name.LocalName == "circle" && shapeWidth != shapeHeight) throw new FormatException("A gradient circle needs equal width and height.");
                        var stops = new[] { new UiGradientStop { Offset = 0, Color = shapeColor }, new UiGradientStop { Offset = 1, Color = endColor } };
                        var gradient = gradientKind == "radial" ? UiGradient.Radial(Length("gradientX", .5, 1), Length("gradientY", .5, 1), stops)
                            : UiGradient.Linear(N("gradientAngle", 90, -360, 360), stops);
                        UiElement paint = new UiModifier { Key = "gradient", Fill = true, Background = gradient,
                            Clip = node.Name.LocalName == "circle" ? "circle" : node.Name.LocalName == "capsule" ? "capsule" : "bounds",
                            Radius = rounded ? (UiSize)Length("cornerRadius", .06) : default,
                            Child = new UiStack { Key = "space", Children = [] } };
                        var gradientLayers = new List<UiElement> { AlphaPaint.Fade(paint, colors["color"].Opacity) };
                        if (attributes.ContainsKey("strokeColor")) gradientLayers.Add(AlphaPaint.Fade(new UiShape { Key = "stroke", Fill = true,
                            Shape = rounded ? "rounded-rectangle" : node.Name.LocalName == "rect" ? "rectangle" : node.Name.LocalName,
                            CornerRadius = Length("cornerRadius", .06), StrokeColor = Color("strokeColor", "#ffffff"), StrokeWidth = Length("strokeWidth", .01) }, colors["strokeColor"].Opacity));
                        body = AlphaPaint.Layer(new UiLayer { Key = "body", MainSize = shapeWidth, Fill = false, Children = gradientLayers.ToArray() });
                    }
                    // Lengths use the widget basis even when nested; no image upload or custom renderer is needed.
                    var horizontalParts = new List<UiElement>();
                    if (shapeX > 0) horizontalParts.Add(new UiStack { Key = "offsetX", MainSize = shapeX, Fill = false, Children = [] });
                    horizontalParts.Add(body);
                    var verticalParts = new List<UiElement>();
                    if (shapeY > 0) verticalParts.Add(new UiStack { Key = "offsetY", MainSize = shapeY, Fill = false, Children = [] });
                    verticalParts.Add(new UiStack { Key = "row", Direction = "horizontal", Justify = "start", Align = "stretch", MainSize = shapeHeight, Fill = false, Gap = 0, Padding = 0, Children = horizontalParts.ToArray() });
                    return new UiStack { Key = key, Direction = "vertical", Justify = "start", Align = "stretch", Gap = 0, Padding = 0,
                        Fill = B("fill", false), MainSize = attributes.ContainsKey("mainSize") ? mainSize : (UiSize)(parentHorizontal ? shapeX + shapeWidth : shapeY + shapeHeight), Children = verticalParts.ToArray() };
                case "clock": return new UiClockDial { Key = key, Fill = fill, MainSize = mainSize,
                    Value = UiTimeReference.InZone(A("zone") is "—" ? null : A("zone")), Seconds = B("seconds", true),
                    Color = attributes.ContainsKey("color") ? UiValue.Of(Color("color", "#ffffff")) : default };
                case "progress-bar": return new UiProgressBar { Key = key, Fill = fill, MainSize = mainSize,
                    Value = Progress(), StartColor = Color("color", "#54dfcc"), EndColor = EndColor(), Thickness = Length("thickness", .05, 1) };
                case "slider":
                    var sliderMin = N("min", 0, -1e12, 1e12); var sliderMax = N("max", 100, -1e12, 1e12);
                    if (sliderMax <= sliderMin) throw new FormatException("For slider, max must be greater than min.");
                    var sliderKey = A("key");
                    if (sliderKey.Length > 0) DataHub.ValidateName(sliderKey);
                    if (B("interactive", false) && sliderKey.Length == 0) throw new FormatException("An interactive slider needs a key to store its value.");
                    if (sliderKey.Length > 0 && attributes.ContainsKey("value")) throw new FormatException("Use either key or value on a slider, not both.");
                    var sliderValue = sliderKey.Length == 0 ? N("value", sliderMin, -1e12, 1e12)
                        : values.TryGetValue(sliderKey, out var bound) && double.TryParse(bound.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric) && double.IsFinite(numeric) ? numeric : sliderMin;
                    var step = N("step", 0, 0, sliderMax - sliderMin);
                    double Snap(double value) => step == 0 ? value : Math.Clamp(sliderMin + Math.Round((value - sliderMin) / step, MidpointRounding.AwayFromZero) * step, sliderMin, sliderMax);
                    UiEventHandler Handler(string name) => UiEventHandler.On(name, e =>
                    {
                        if (!e.TryGetDouble(out var level) || !double.IsFinite(level) || level < 0 || level > 1) return UiEventOutcome.Rejected("Expected a level between 0 and 1.");
                        var value = Snap(sliderMin + level * (sliderMax - sliderMin));
                        input!(new(key, name, value, (value - sliderMin) / (sliderMax - sliderMin), sliderKey));
                        return UiEventOutcome.Accepted;
                    });
                    return new UiSlider { Key = key, Fill = fill, MainSize = mainSize,
                        Level = Math.Clamp((Snap(sliderValue) - sliderMin) / (sliderMax - sliderMin), 0, 1),
                        Step = step > 0 ? UiValue.Of(step / (sliderMax - sliderMin)) : default,
                        Events = input != null && B("interactive", false) ? [Handler(UiComponentEvents.Adjust), Handler(UiComponentEvents.Change)] : [],
                        Direction = Choice("direction", "horizontal"),
                        LevelColor = Color("color", "#54dfcc"), Thickness = Length("thickness", .04, 1) };
                case "stack" when attributes.ContainsKey("borderStyle") || attributes.ContainsKey("borderColor"):
                    var border = Choice("borderStyle", "static");
                    if (border is not ("static" or "none") && (DisplayColor.Parse(A("background", "#1a1a1a")).Alpha < 255 || DisplayColor.Parse(A("borderColor", "#54dfcc")).Alpha < 255))
                        throw new FormatException("Animated border colors must be opaque. Use opacity to fade the whole component.");
                    if (border == "none") return new UiStack { Key = key, Children = children, Fill = fill, MainSize = mainSize,
                        Direction = Choice("direction", "vertical"), Justify = Choice("justify", "start"), Align = Choice("align", "stretch"), Gap = Length("gap", 0), Padding = Length("padding", 0),
                        Background = attributes.ContainsKey("background") ? UiValue.Of(Color("background", "#1a1a1a")) : default };
                    return new UiButton { Key = key, Children = children, Fill = fill, MainSize = mainSize,
                        Direction = Choice("direction", "vertical"), Justify = Choice("justify", "start"),
                        Align = Choice("align", "stretch"), Gap = Length("gap", 0), Padding = Length("padding", 0),
                        Background = Color("background", "#1a1a1a"),
                        BorderStyle = border == "none" ? default : UiValue.Of(border), BorderColor = Color("borderColor", "#54dfcc"),
                        Corner = node.Parent == null ? UiValue.Of("tile") : default };
                case "stack": return new UiStack { Key = key, Children = children, Fill = fill, MainSize = mainSize,
                    Direction = Choice("direction", "vertical"), Justify = Choice("justify", "start"),
                    Align = Choice("align", "stretch"), Gap = Length("gap", 0), Padding = Length("padding", 0),
                    Background = !attributes.ContainsKey("background") ? default : UiValue.Of(Color("background", "#1a1a1a")) };
                case "layer":
                    var layers = children;
                    if (attributes.ContainsKey("background"))
                    {
                        var paint = new UiShape { Key = "_background", Fill = true, Shape = "rectangle", Color = Color("background", "#1a1a1a") };
                        if (colors["background"].Alpha > 0) layers = new[] { AlphaPaint.Fade(paint, colors["background"].Opacity) }.Concat(children).ToArray();
                    }
                    return AlphaPaint.Layer(new UiLayer { Key = key, Children = layers, Fill = fill, MainSize = mainSize });
                case "text": return new UiTextRun { Key = key, Text = Expand(string.Concat(node.Nodes().OfType<XText>().Select(t => t.Value)), values), Size = TextLength(textSize), MinSize = TextLength(minTextSize),
                    Weight = Choice("weight", "regular"), Color = attributes.ContainsKey("role") && !attributes.ContainsKey("color") ? UiValue.None<string>() : UiValue.Of(Color("color", "#ffffff")), Align = Choice("align", "start"),
                    Role = Choice("role", "primary"), FontFace = A("fontFace") is var face && face is not ("" or "—") ? UiValue.Of(face) : UiValue.None<string>(),
                    Digits = attributes.ContainsKey("digits") ? UiValue.Of(N("digits", 0, 0, 32)) : UiValue.None<double>(),
                    Wrap = B("wrap", false), MaxLines = Integer("maxLines", 1, 1, 8), Fill = fill, MainSize = mainSize };
                case "chart":
                    var dataKey = A("key"); DataHub.ValidateName(dataKey);
                    var low = N("min", 0, -1e12, 1e12); var high = N("max", 100, -1e12, 1e12);
                    if (high <= low) throw new FormatException("For chart, max must be greater than min.");
                    var count = N("points", 60, 2, DataHub.HistoryLimit);
                    if (count != Math.Truncate(count)) throw new FormatException("For chart, points must be an integer.");
                    var samples = history?.Invoke(dataKey, (int)count) ?? [];
                    return new UiChart { Key = key, Fill = fill, MainSize = mainSize,
                        Points = UiValue.Of<IReadOnlyList<double>>(samples.Select(v => Math.Clamp((v - low) / (high - low), 0, 1)).ToArray()),
                        Color = Color("color", "#54dfcc"), PlotTop = Length("plotTop", .66, 1), Thickness = Length("thickness", .015, 1) };
                case "bar":
                    var min = N("min", 0, -1e12, 1e12); var max = N("max", 100, -1e12, 1e12);
                    if (max <= min) throw new FormatException("For bar, max must be greater than min.");
                    return new UiRangeBar { Key = key, Start = Math.Clamp((N("start", min, -1e12, 1e12) - min) / (max - min), 0, 1), End = Math.Clamp((N("value", min, -1e12, 1e12) - min) / (max - min), 0, 1),
                        Marker = attributes.ContainsKey("marker") ? UiValue.Of(Math.Clamp((N("marker", min, -1e12, 1e12) - min) / (max - min), 0, 1)) : default,
                        StartColor = Color("color", "#54dfcc"), EndColor = EndColor(), Thickness = Length("thickness", .05, 1), Fill = fill, MainSize = mainSize };
                default: throw new FormatException("Unsupported component.");
            }
        }
        var result = Build();
        if (result == null) return null;
        if (node.Name.LocalName == "line" && Choice("gradient", "none") is var lineGradient && lineGradient != "none")
        {
            var start = colors["color"];
            var end = Color("endColor", start.Hex);
            if (start.Alpha != colors["endColor"].Alpha)
                throw new FormatException("Use the same alpha for both gradient colors. Use opacity to fade the entire shape.");
            var stops = new[] { new UiMaskStop { Offset = 0, Opacity = 0 }, new UiMaskStop { Offset = 1, Opacity = 1 } };
            var mask = lineGradient == "radial"
                ? UiMask.Radial(Length("gradientX", .5, 1), Length("gradientY", .5, 1), stops)
                : UiMask.Linear(N("gradientAngle", 90, -360, 360), stops);
            result = GradientLine(result, start.Rgb, end, start.Opacity, mask, attributes.ContainsKey("angle") || attributes.ContainsKey("x1"));
        }
        if (result is UiTextRun textRun && Integer("maxLines", 1, 1, 8) is var lines && lines > 1)
        {
            // beta.11 gives line-clamped text extra vertical ink padding. A separate clipping
            // frame prevents that padding exposing a sliver of the following line.
            var height = textSize * lines * 1.2;
            var maxHeight = attributes.ContainsKey("sizeCap") ? UiLength.Capped(height, N("sizeCap", 32, 1, 256) * lines * 1.2) : UiLength.OfBasis(height);
            result = new UiModifier { Key = key, MainSize = mainSize, Fill = fill, Clip = "bounds",
                Frame = new UiFrame { MaxHeight = maxHeight }, Child = textRun with { Key = "text", MainSize = default, Fill = default } };
        }
        if (node.Name.LocalName is not ("image" or "svg" or "modifier"))
            result = AlphaPaint.Fade(AlphaPaint.Apply(result, colors), N("opacity", 1), animated.Contains("opacity") && duration > 0);
        if (input != null && (node.Name.LocalName is "stack" or "layer") && B("interactive", false))
            result = result with { Events = new[] { UiComponentEvents.Press, UiComponentEvents.LongPress, UiComponentEvents.PressStart, UiComponentEvents.PressEnd }
                .Select(name => UiEventHandler.On(name, () => input(new(key, name)))).ToArray() };
        return result;
    }

    // Paint an opaque base and a masked end colour, then apply alpha once to the group.
    // Both copies retain identical geometry, including local coordinates and flat ends.
    private static UiElement GradientLine(UiElement source, string start, string end, double opacity, UiMask mask, bool stroke)
    {
        UiElement Paint(UiElement element, string color) => element switch
        {
            UiShape shape => stroke ? shape with { StrokeColor = color } : shape with { Color = color },
            UiModifier modifier => modifier with { Opacity = 1, Child = Paint(modifier.Child!, color) },
            UiStack stack => stack with { Children = stack.Children.Select(child => Paint(child, color)).ToArray() },
            _ => throw new InvalidOperationException("Unexpected line paint component.")
        };
        var size = source is UiComponentLeaf leaf ? leaf.MainSize : ((UiComponentContainer)source).MainSize;
        var fill = source is UiComponentLeaf leafFill ? leafFill.Fill : ((UiComponentContainer)source).Fill;
        UiElement Copy(string color, bool expand) => Paint(source, color) switch
        {
            UiComponentLeaf child => child with { Key = "line", MainSize = default, Fill = expand ? UiValue.Of(true) : default },
            UiComponentContainer child => child with { Key = "line", MainSize = default, Fill = expand ? UiValue.Of(true) : default },
            _ => throw new InvalidOperationException("Unexpected line layout component.")
        };
        return AlphaPaint.Fade(AlphaPaint.Layer(new UiLayer { Key = source.Key, MainSize = size, Fill = fill,
            Children = [new UiStack { Key = "start", Fill = true, Children = [Copy(start, true)] },
                new UiModifier { Key = "end", Fill = true, Mask = mask, Child = Copy(end, false) }] }), opacity);
    }
}


