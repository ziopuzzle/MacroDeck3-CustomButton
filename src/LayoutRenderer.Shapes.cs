using MacroDeck.Ui.Components;
using MacroDeck.Ui.Dsl;

namespace Ziopuzzle.CustomButton;

public sealed partial class LayoutRenderer
{
    private sealed partial class ComponentRenderer
    {
        private UiElement? BuildPolygon()
        {
            if (node.Name.LocalName == "sector" && (Length("radius", .4, 1) == 0 || Number("sweepAngle", 120, -360, 360) == 0)) return null;
            var path = node.Name.LocalName == "polygon" ? ShapePaths.Polygon(Attribute("points", "10%,90%;50%,10%;90%,90%"))
                : ShapePaths.Sector(Length("cx", .5, 1), Length("cy", .5, 1), Length("radius", .4, 1), Number("startAngle", -90, -360, 360), Number("sweepAngle", 120, -360, 360));
            var localCanvas = Choice("coordinates", "widget") == "local";
            return new UiShape { Key = key, Shape = "path", Path = path, Color = Color("color", "#54dfcc"),
                StrokeColor = attributes.ContainsKey("strokeColor") ? UiValue.Of(Color("strokeColor", "#ffffff")) : default,
                StrokeWidth = Length("strokeWidth", .01), Fill = Boolean("fill", localCanvas), MainSize = localCanvas || attributes.ContainsKey("mainSize") ? mainSize : (UiSize)1 };
        }

        private UiElement? BuildShape()
        {
            var isLine = node.Name.LocalName == "line";
            var localCoordinates = Choice("coordinates", "widget") == "local";
            if (isLine && attributes.ContainsKey("angle"))
            {
                if (new[] { "x", "y", "direction", "x1", "y1", "x2", "y2" }.Any(attributes.ContainsKey))
                    throw new FormatException("Use cx/cy/length/angle instead of endpoints or x/y/direction for an angular line.");
                var cx = Length("cx", .5, 1); var cy = Length("cy", .5, 1);
                var length = Length("length", .4, 1);
                var radians = Number("angle", 0, -3600, 3600) * Math.PI / 180;
                var thickness = Length("thickness", .02, 1);
                if (length == 0 || thickness == 0) return null;
                var lineColor = Color("color", "#54dfcc");
                return AlphaPaint.Fade(new UiShape { Key = key, Shape = "path",
                    Path = FormattableString.Invariant($"M{cx} {cy} L{cx + length * Math.Cos(radians)} {cy + length * Math.Sin(radians)}"),
                    StrokeColor = lineColor, StrokeWidth = thickness, Fill = Boolean("fill", localCoordinates),
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
                    StrokeWidth = thickness, Fill = Boolean("fill", localCoordinates), MainSize = localCoordinates || attributes.ContainsKey("mainSize") ? mainSize : (UiSize)1 }, colors["color"].Opacity);
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
                    rounded ? Length("cornerRadius", .06) : 0, Attribute("data", "M0 0 L1 0.5 L0 1 Z"));
                return new UiShape { Key = key, Shape = "path", Path = localPath, Color = shapeColor,
                    Fill = Boolean("fill", true), MainSize = mainSize,
                    StrokeColor = attributes.ContainsKey("strokeColor") ? UiValue.Of(Color("strokeColor", "#ffffff")) : default,
                    StrokeWidth = Length("strokeWidth", .01) };
            }
            UiElement body = AlphaPaint.Apply(new UiShape { Key = "body", MainSize = shapeWidth, Fill = false,
                Shape = rounded ? "rounded-rectangle" : node.Name.LocalName is "circle" or "capsule" or "path" ? node.Name.LocalName : "rectangle",
                CornerRadius = Length("cornerRadius", .06), Color = shapeColor,
                Path = node.Name.LocalName == "path" ? UiValue.Of(ShapePaths.Validate(Attribute("data", "M0 0 L1 0.5 L0 1 Z"))) : default,
                StrokeColor = attributes.ContainsKey("strokeColor") ? UiValue.Of(Color("strokeColor", "#ffffff")) : default,
                StrokeWidth = Length("strokeWidth", .01) }, colors);
            if (node.Name.LocalName is "rect" or "circle" or "capsule" && Choice("gradient", "none") is var gradientKind && gradientKind != "none")
            {
                var endColor = Color("endColor", colors["color"].Hex);
                if (colors["color"].Alpha != colors["endColor"].Alpha) throw new FormatException("Use the same alpha for both gradient colors. Use opacity to fade the entire shape.");
                if (node.Name.LocalName == "circle" && shapeWidth != shapeHeight) throw new FormatException("A gradient circle needs equal width and height.");
                var stops = new[] { new UiGradientStop { Offset = 0, Color = shapeColor }, new UiGradientStop { Offset = 1, Color = endColor } };
                var gradient = gradientKind == "radial" ? UiGradient.Radial(Length("gradientX", .5, 1), Length("gradientY", .5, 1), stops)
                    : UiGradient.Linear(Number("gradientAngle", 90, -360, 360), stops);
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
                Fill = Boolean("fill", false), MainSize = attributes.ContainsKey("mainSize") ? mainSize : (UiSize)(parentHorizontal ? shapeX + shapeWidth : shapeY + shapeHeight), Children = verticalParts.ToArray() };
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
}
