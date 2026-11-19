using System.Globalization;
using MacroDeck.Ui.Components;
using MacroDeck.Ui.Dsl;

namespace Ziopuzzle.CustomButton;

public sealed partial class LayoutRenderer
{
    private sealed partial class ComponentRenderer
    {
        private UiElement BuildResponsive()
        {
            var branches = node.Elements("variant").ToArray();
            var variants = new List<UiResponsiveVariant>();
            for (var i = 1; i < branches.Length; i++)
            {
                var branch = branches[i];
                try
                {
                    var settings = ResolveAttributes(branch, values);
                    double? Bound(string name)
                    {
                        if (!settings.TryGetValue(name, out var raw)) return null;
                        try
                        {
                            var text = Expand(raw, values).Trim();
                            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value)
                                || value < 0 || (name.EndsWith("Aspect") && value == 0)) throw new FormatException($"{name} needs a nonnegative cell count or positive aspect ratio.");
                            return value;
                        }
                        catch (FormatException error) { throw AttributeError(name, raw, error); }
                    }
                    var minW = Bound("minWidth"); var maxW = Bound("maxWidth"); var minH = Bound("minHeight"); var maxH = Bound("maxHeight");
                    var minA = Bound("minAspect"); var maxA = Bound("maxAspect");
                    if (minW >= maxW || minH >= maxH || minA >= maxA) throw new FormatException("Responsive maximum bounds must exceed minimum bounds.");
                    variants.Add(new UiResponsiveVariant { Content = new UiLayer { Key = "branch" + i, Children = [children[i]] },
                        MinWidth = minW, MaxWidth = maxW, MinHeight = minH, MaxHeight = maxH, MinAspect = minA, MaxAspect = maxA });
                }
                catch (FormatException error) when (!error.Data.Contains("LayoutLine")) { throw LocatedError(branch, error); }
            }
            return new UiResponsive { Key = key, Fill = fill, MainSize = mainSize,
                Default = new UiLayer { Key = "default", Children = children.Take(1).ToArray() }, Variants = variants,
                Fallback = new UiTextRun { Key = "unsupported", Shadow = false, Text = "Responsive layout requires a compatible client." } };
        }

        private UiElement BuildVariant()
        {
            return AlphaPaint.Layer(new UiLayer { Key = key, Fill = fill, MainSize = mainSize, Children = children });
        }

        private UiElement BuildModifier()
        {
            UiLength? FrameLength(string name) => attributes.ContainsKey(name) ? UiLength.OfBasis(Length(name, 0)) : null;
            var frame = new UiFrame { Width = FrameLength("width"), Height = FrameLength("height"),
                MinWidth = FrameLength("minWidth"), MaxWidth = FrameLength("maxWidth"), MinHeight = FrameLength("minHeight"), MaxHeight = FrameLength("maxHeight") };
            if (attributes.ContainsKey("minWidth") && attributes.ContainsKey("maxWidth") && Length("minWidth", 0) > Length("maxWidth", 0)
                || attributes.ContainsKey("minHeight") && attributes.ContainsKey("maxHeight") && Length("minHeight", 0) > Length("maxHeight", 0))
                throw new FormatException("Modifier maximum sizes must not be smaller than minimum sizes.");
            return new UiModifier { Key = key, Fill = fill, MainSize = mainSize, Padding = Length("padding", 0),
                Opacity = Number("opacity", 1), Radius = Length("radius", 0), Disabled = Boolean("disabled", false),
                Clip = Choice("clip", "none") == "none" ? default : UiValue.Of(Choice("clip", "none")), Frame = frame,
                Child = AlphaPaint.Layer(new UiLayer { Key = "content", Children = children }) };
        }

        private UiElement BuildTransform()
        {
            return new UiTransform { Key = key, Fill = fill, MainSize = mainSize, Children = children,
                // Normalize only the rendered angle: the animation must retain the full
                // unwrapped target to preserve direction and multi-turn transitions.
                Rotation = Number("rotation", 0, -1e12, 1e12) % 360, OriginX = Number("originX", .5, -1e12, 1e12), OriginY = Number("originY", .5, -1e12, 1e12),
                Zoom = Number("zoom", 1, 0.001, 1e12), OffsetX = Number("offsetX", 0, -1e12, 1e12), OffsetY = Number("offsetY", 0, -1e12, 1e12) };
        }

        private UiElement BuildBorderedStack()
        {
            var border = Choice("borderStyle", "static");
            if (border is not ("static" or "none") && (ParseColor("background", "#1a1a1a").Alpha < 255 || ParseColor("borderColor", "#54dfcc").Alpha < 255))
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
        }

        private UiElement BuildScroll()
        {
            var scroll = new UiList { Key = key, Fill = fill, MainSize = mainSize, Children = children, Direction = Choice("direction", "vertical"), Gap = Length("gap", 0), Padding = Length("padding", 0), Background = !attributes.ContainsKey("background") ? default : UiValue.Of(Color("background", "#1a1a1a")) };
            if (!attributes.ContainsKey("anchor") && followAnchor == null) return scroll;
            if (Choice("direction", "vertical") != "vertical") throw new FormatException("Scroll anchor is supported only in the vertical direction.");
            // Keep version and fallback stable while toggling the anchor and changing rows.
            // A changing fallback would make TreeChanges replace the list and lose its position.
            return scroll with { Anchor = followAnchor ?? Choice("anchor", "start"), RequiredComponentVersion = 3,
                Fallback = new UiTextRun { Key = "unsupported", Text = "Scroll anchoring requires List component version 3.", Shadow = false } };
        }

        private UiElement BuildStack()
        {
            return new UiStack { Key = key, Children = children, Fill = fill, MainSize = mainSize,
            Direction = Choice("direction", "vertical"), Justify = Choice("justify", "start"),
            Align = Choice("align", "stretch"), Gap = Length("gap", 0), Padding = Length("padding", 0),
            Background = !attributes.ContainsKey("background") ? default : UiValue.Of(Color("background", "#1a1a1a")) };
        }

        private UiElement BuildLayer()
        {
            var layers = children;
            if (attributes.ContainsKey("background"))
            {
                var paint = new UiShape { Key = "_background", Fill = true, Shape = "rectangle", Color = Color("background", "#1a1a1a") };
                if (colors["background"].Alpha > 0) layers = new[] { AlphaPaint.Fade(paint, colors["background"].Opacity) }.Concat(children).ToArray();
            }
            return AlphaPaint.Layer(new UiLayer { Key = key, Children = layers, Fill = fill, MainSize = mainSize });
        }
    }
}
