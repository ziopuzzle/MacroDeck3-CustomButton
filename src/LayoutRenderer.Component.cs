using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using MacroDeck.Ui.Components;
using MacroDeck.Ui.Dsl;

namespace Ziopuzzle.CustomButton;

public sealed partial class LayoutRenderer
{
    // Never reuse across nodes or frames: event handlers retain this component's data and scope.
    private sealed partial class ComponentRenderer(LayoutRenderer owner, XElement node,
        IReadOnlyDictionary<string, JsonElement> values, bool parentHorizontal, RenderContext context, RepeatScope? scope)
    {
        private readonly string elementId = (string)node.Attribute("id")!;
        private readonly string key = scope?.NodeKey((string)node.Attribute("id")!) ?? (string)node.Attribute("id")!;
        private readonly Dictionary<string, string> attributes = ResolveAttributes(node, values);
        private readonly Dictionary<string, DisplayColor> colors = new();
        private double duration;
        private string easing = "";
        private string colorSpace = "";
        private HashSet<string> animated = null!;
        private bool fill, horizontal;
        private UiSize mainSize;
        private string? followAnchor;
        private UiElement[] children = [];
        private double textSize, minTextSize;

        public UiElement? Render()
        {
            var durationText = Attribute("transitionMs", "0");
            if (!double.TryParse(durationText, NumberStyles.Float, CultureInfo.InvariantCulture, out duration) || !double.IsFinite(duration) || duration < 0 || duration > 10000)
                throw AttributeError("transitionMs", "transitionMs must be between 0 and 10000 milliseconds.");
            easing = Attribute("easing", "ease-out");
            colorSpace = Attribute("colorSpace", "linear-rgb");
            if (!LayoutOptions.Choices(node.Name.LocalName, "colorSpace").Contains(colorSpace)) throw AttributeError("colorSpace", "Use linear-rgb, srgb or hsv for colorSpace.");
            if (!LayoutOptions.Choices(node.Name.LocalName, "easing").Contains(easing)) throw AttributeError("easing", "Use linear, ease-in, ease-out or ease-in-out easing.");
            animated = Attribute("transitionProperties", "opacity").Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
            var supported = LayoutOptions.TransitionProperties(node.Name.LocalName);
            foreach (var property in animated)
                if (!supported.Contains(property))
                    throw AttributeError("transitionProperties", $"Transition property {property} is not supported on {node.Name}.");
            if (!Boolean("visible", true) || attributes.TryGetValue("visibleWhen", out var visibleWhen) && !DisplayCondition.Evaluate(visibleWhen, values)) return null;
            if (scope != null && Boolean("interactive", false) && node.Name.LocalName is "slider" or "dial" or "toggle" or "text-field" or "segmented" or "trackpad")
                foreach (var name in new[] { "key", "keyX", "keyY" })
                {
                    var boundKey = Attribute(name);
                    if (scope.ReadOnlyNames.Any(n => boundKey == n || boundKey.StartsWith(n + ".", StringComparison.Ordinal)))
                        throw new FormatException("Repeat item/index bindings are read-only. For interactive controls, use a separate Channel key, such as volume_{{itemKey}}.");
                }
            fill = Boolean("fill", node.Parent == null || parentHorizontal || node.Name.LocalName is "scroll" or "layer" or "chart" or "clock" or "trackpad" or "slider" or "dial" or "toggle" or "segmented" or "text-field" or "gauge" or "transform" or "modifier" or "responsive");
            // Keep omitted sizes unset; a numeric ternary would turn default into an explicit zero.
            if (attributes.ContainsKey("mainSize") && Attribute("mainSize") != "auto") mainSize = Length("mainSize", 0);
            horizontal = node.Name.LocalName is "stack" or "repeat" && Choice("direction", "vertical") == "horizontal";
            followAnchor = node.Name == "scroll" ? owner.FollowScroll(node, attributes, values, scope != null) : null;
            children = node.Name == "repeat"
                ? RepeatItems(attributes["items"], attributes.GetValueOrDefault("itemKey", ""), elementId, values, scope, RepeatAliases.Read(node))
                    .Take(owner.scrollLimits.GetValueOrDefault(elementId, int.MaxValue))
                    .SelectMany(item => RenderChildren(item.Values, item.Scope)).ToArray()
                : RenderChildren(values, scope);
            textSize = node.Name.LocalName is "image" or "svg" && Choice("frame", "square") == "container" ? .18 : Length("size", .18, 1);
            minTextSize = Math.Min(Length("minSize", Math.Min(.08, textSize), 1), textSize);
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
                    : UiMask.Linear(Number("gradientAngle", 90, -360, 360), stops);
                result = GradientLine(result, start.Rgb, end, start.Opacity, mask, attributes.ContainsKey("angle") || attributes.ContainsKey("x1"));
            }
            if (result is UiTextRun textRun && Integer("maxLines", 1, 1, 8) is var lines && lines > 1)
            {
                // beta.11 gives line-clamped text extra vertical ink padding. A separate clipping
                // frame prevents that padding exposing a sliver of the following line.
                var height = textSize * lines * 1.2;
                var maxHeight = attributes.ContainsKey("sizeCap") ? UiLength.Capped(height, Number("sizeCap", 32, 1, 256) * lines * 1.2) : UiLength.OfBasis(height);
                result = new UiModifier { Key = key, MainSize = mainSize, Fill = fill, Clip = "bounds",
                    Frame = new UiFrame { MaxHeight = maxHeight }, Child = textRun with { Key = "text", MainSize = default, Fill = default } };
            }
            if (node.Name.LocalName is not ("image" or "svg" or "modifier"))
                result = AlphaPaint.Fade(AlphaPaint.Apply(result, colors), Number("opacity", 1), animated.Contains("opacity") && duration > 0);
            if (context.Modal && (node.Name.LocalName is "stack" or "layer") && Boolean("closeModal", false) && result is UiComponentContainer closeContainer)
            {
                // The client consumes this press to settle its own modal; do not publish an action event.
                var answer = Expand(attributes.GetValueOrDefault("result", ""), values);
                if (answer.Length > 4096) throw new FormatException("Modal result must not exceed 4096 characters.");
                result = closeContainer with { Answer = context.ModalAnswer?.Invoke(elementId, answer) ?? "close", Events = [UiEventHandler.On(UiComponentEvents.Press, () => { })] };
            }
            else if (context.Input != null && (node.Name.LocalName is "stack" or "layer") && Boolean("interactive", false))
                result = result with { Events = new[] { UiComponentEvents.Press, UiComponentEvents.LongPress, UiComponentEvents.PressStart, UiComponentEvents.PressEnd }
                    .Select(name => UiEventHandler.On(name, () => Emit(new(elementId, name)))).ToArray() };
            return result;
        }

        private static Dictionary<string, string> ResolveAttributes(XElement element, IReadOnlyDictionary<string, JsonElement> data)
        {
            var result = element.Attributes().ToDictionary(a => a.Name.LocalName, a => a.Value);
            foreach (var style in element.Elements("style"))
                if (DisplayCondition.Evaluate((string)style.Attribute("when")!, data))
                    foreach (var attribute in style.Attributes().Where(a => a.Name != "when"))
                        result[attribute.Name.LocalName] = attribute.Value;
            return result;
        }

        private void Emit(ControlInput value) => context.Input?.Invoke(scope == null ? value : value with { RepeatedItem = scope.Item });

        private UiElement[] RenderChildren(IReadOnlyDictionary<string, JsonElement> data, RepeatScope? childScope)
        {
            var childContext = node.Name == "segmented" ? context with { Input = null, Modal = false } : context;
            return node.Elements().Where(e => e.Name != "style")
                .Select(child => owner.Make(child, data, horizontal, childContext, childScope)
                    ?? (node.Name.LocalName is "responsive" or "segmented"
                        ? new UiLayer { Key = childScope?.NodeKey((string)child.Attribute("id")!) ?? (string)child.Attribute("id")!, Children = [] }
                        : null))
                .OfType<UiElement>().ToArray();
        }

        private UiElement? Build() => node.Name.LocalName switch
        {
            "responsive" => BuildResponsive(),
            "variant" => BuildVariant(),
            "modifier" => BuildModifier(),
            "dynamic-text" => BuildDynamicText(),
            "transform" => BuildTransform(),
            "icon" => BuildIcon(),
            "gauge" => BuildGauge(),
            "image" or "svg" => BuildImage(),
            "polygon" or "sector" => BuildPolygon(),
            "rect" or "circle" or "capsule" or "path" or "line" => BuildShape(),
            "clock" => BuildClock(),
            "progress-bar" => BuildProgressBar(),
            "trackpad" => BuildTrackpad(),
            "text-field" => BuildTextField(),
            "toggle" or "segmented" => BuildSelection(),
            "dial" or "slider" => BuildSlider(),
            "stack" when attributes.ContainsKey("borderStyle") || attributes.ContainsKey("borderColor") => BuildBorderedStack(),
            "scroll" => BuildScroll(),
            "repeat" or "stack" => BuildStack(),
            "layer" => BuildLayer(),
            "text" => BuildText(),
            "chart" => BuildChart(),
            "bar" => BuildBar(),
            _ => throw new FormatException("Unsupported component.")
        };

        private FormatException AttributeError(string name, string message) => AttributeError(name, new FormatException(message));
        private FormatException AttributeError(string name, FormatException error) =>
            AttributeError(name, attributes.GetValueOrDefault(name, ""), error);
        private static FormatException AttributeError(string name, string raw, FormatException error)
        {
            error.Data["LayoutAttribute"] = name;
            // These attributes already include active styles and Repeat-local evaluation.
            error.Data["LayoutAttributeValue"] = raw;
            return error;
        }
        private string Attribute(string name, string fallback = "")
        {
            try { return Expand(attributes.GetValueOrDefault(name, fallback), values).Trim(); }
            catch (FormatException error) { throw AttributeError(name, error); }
        }
        private double Animate(string name, double value) => duration > 0 && animated.Contains(name) && context.Animation != null
            ? context.Animation.Value(key + "." + name, value, duration, easing) : value;
        private double Number(string name, double fallback, double min = 0, double max = 1)
        {
            var raw = Attribute(name);
            if (raw is "" or "—") return Animate(name, fallback);
            var validNumber = double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n);
            if (!validNumber || n < min || n > max)
            {
                var error = new FormatException($"{node.Attribute("id")?.Value}: {name} must be a number between {min} and {max}.");
                // Step limits depend on the other axis/range attributes, which may be bound.
                if (validNumber && n > max && name is "step" or "stepX" or "stepY") throw error;
                throw AttributeError(name, error);
            }
            return Animate(name, n);
        }
        private bool Boolean(string name, bool fallback)
        {
            var raw = Attribute(name);
            if (raw is "" or "—") return fallback;
            if (bool.TryParse(raw, out var b)) return b;
            if (raw is "1" or "0") return raw == "1";
            throw AttributeError(name, $"{name} must be true or false.");
        }
        private double Length(string name, double fallback, double max = 4)
        {
            var raw = Attribute(name);
            if (raw is "" or "auto" or "—" or "—%") return Animate(name, fallback);
            bool percent = raw.EndsWith('%');
            if (percent) raw = raw[..^1];
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || !double.IsFinite(n)) throw AttributeError(name, $"Specify {name} as 0.2 or 20%.");
            n = percent ? n / 100 : n;
            if (n < 0 || n > max) throw AttributeError(name, $"{name} must be between 0 and {max}.");
            return Animate(name, n);
        }
        private int Integer(string name, int fallback, int min, int max)
        {
            var n = Number(name, fallback, min, max);
            return n == Math.Truncate(n) ? (int)n : throw AttributeError(name, $"{name} must be an integer.");
        }
        private string Choice(string name, string fallback)
        {
            var options = LayoutOptions.Choices(node.Name.LocalName, name);
            var value = Attribute(name, fallback).ToLowerInvariant();
            value = value switch { "row" when name == "direction" => "horizontal", "column" when name == "direction" => "vertical", "left" or "top" when name is "align" or "justify" => "start", "right" or "bottom" when name is "align" or "justify" => "end", _ => value };
            return options.Contains(value) ? value : throw AttributeError(name, $"{name} must be one of {string.Join('/', options)}.");
        }
        private DisplayColor ParseColor(string name, string fallback)
        {
            try { return DisplayColor.Parse(Attribute(name, fallback)); }
            catch (FormatException error) { throw AttributeError(name, error); }
        }
        private string Color(string name, string fallback)
        {
            var color = ParseColor(name, fallback);
            if (duration > 0 && animated.Contains(name) && context.Animation != null)
            {
                // Keep alpha/structure constant during RGB interpolation. Use opacity for fades.
                var rgb = context.Animation.Color(key + "." + name, color.Rgb, duration, easing, colorSpace);
                color = new DisplayColor(rgb, color.Alpha);
            }
            colors[name] = color;
            return color.Rgb;
        }
        private string EndColor() => Color("endColor", colors["color"].Hex);
        private UiSize TextLength(double size) => attributes.ContainsKey("sizeCap") ? UiSize.Capped(size, Number("sizeCap", 32, 1, 256)) : (UiSize)size;
    }
}
