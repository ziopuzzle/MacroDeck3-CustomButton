using MacroDeck.Ui.Components;
using MacroDeck.Ui.Dsl;

namespace Ziopuzzle.CustomButton;

/// <summary>Separates paint opacity from layout and from independently coloured children.</summary>
public static class AlphaPaint
{
    // beta.11's .widget-modifier position:relative overrides .widget-layer > *.
    // A stack keeps the direct layer child absolutely positioned, with the modifier inside.
    public static UiLayer Layer(UiLayer layer) => layer with { Children = layer.Children.Select(child => child is UiModifier modifier
        ? (UiElement)new UiStack { Key = child.Key, Fill = true, Align = "stretch", Children = [modifier with { Key = "opacity", MainSize = default, Fill = true }] }
        : child).ToArray() };
    public static UiElement Fade(UiElement element, double opacity, bool force = false)
    {
        if (opacity == 1 && !force) return element;
        var (size, fill) = Layout(element);
        var child = element switch
        {
            UiComponentLeaf leaf => leaf with { Key = "paint", MainSize = default, Fill = default, ColumnSpan = default, RowSpan = default },
            UiComponentContainer container => container with { Key = "paint", MainSize = default, Fill = default, ColumnSpan = default, RowSpan = default },
            _ => element with { Key = "paint" }
        };
        return new UiModifier { Key = element.Key, MainSize = size, Fill = fill, Opacity = opacity,
            Child = child };
    }
    private static (UiSize Size, UiValue<bool> Fill) Layout(UiElement element) => element switch
    {
        UiComponentLeaf leaf => (leaf.MainSize, leaf.Fill),
        UiComponentContainer container => (container.MainSize, container.Fill),
        _ => (default, default)
    };
    public static UiElement Apply(UiElement element, IReadOnlyDictionary<string, DisplayColor> colors)
    {
        if (element is UiModifier { Child: UiTextRun or UiIcon } textFrame)
            return textFrame with { Child = Apply(textFrame.Child, colors) };
        double A(string name) => colors.TryGetValue(name, out var c) ? c.Opacity : 1;
        if (element is UiShape shape)
        {
            if (!colors.ContainsKey("strokeColor")) return Fade(shape, A("color"));
            if (A("color") == A("strokeColor")) return Fade(shape, A("color"));
            return Layer(new UiLayer { Key = shape.Key, Fill = shape.Fill, MainSize = shape.MainSize, Children = [
                Fade(shape with { Key = "fill", MainSize = default, Fill = true, StrokeColor = default, StrokeWidth = default }, A("color")),
                Fade(shape with { Key = "stroke", MainSize = default, Fill = true, Color = default }, A("strokeColor"))] });
        }
        if (element is UiRangeBar or UiProgressBar)
        {
            if (A("color") != A("endColor")) throw new FormatException("Use the same alpha for both ends of a bar.");
            return Fade(element, A("color"));
        }
        if (element is UiTextRun or UiDynamicText or UiChart or UiClockDial or UiSlider or UiGauge or UiIcon) return Fade(element, A("color"));
        if (element is UiStack stack && A("background") != 1)
        {
            if (A("background") == 0) return stack with { Background = default };
            // Layer children are absolute, so an empty background collapses in an auto-sized
            // row. Keep one relative modifier in normal flow, with an invisible, noninteractive
            // sizing copy. The foreground retains its own alpha and is painted only once.
            var background = stack with { Key = "background", MainSize = default, Fill = true,
                Children = stack.Children.Select(c => Fade(MeasurementCopy(c), 0)).ToArray() };
            return new UiLayer { Key = stack.Key, MainSize = stack.MainSize, Fill = stack.Fill, Children = [
                Fade(background, A("background")),
                stack with { Key = "content", MainSize = default, Fill = true, Background = default }] };
        }
        if (element is UiButton button && (A("background") != 1 || A("borderColor") != 1))
        {
            var content = new UiStack { Key = "content", Fill = button.Fill, MainSize = button.MainSize,
                Direction = button.Direction, Align = button.Align, Justify = button.Justify, Padding = button.Padding, Gap = button.Gap, Children = button.Children };
            // The host's animated button border cannot be separated from its opaque default background.
            // Static borders and backgrounds use shape layers so child opacity is never changed.
            return Layer(new UiLayer { Key = button.Key, Fill = button.Fill, MainSize = button.MainSize, Children = [
                Fade(new UiShape { Key = "background", Fill = true, Shape = "rounded-rectangle", CornerRadius = .06, Color = button.Background }, A("background")),
                content,
                Fade(new UiShape { Key = "border", Fill = true, Shape = "rounded-rectangle", CornerRadius = .06, StrokeColor = button.BorderColor, StrokeWidth = .015 }, A("borderColor"))] });
        }
        return element;
    }
    private static UiElement MeasurementCopy(UiElement element)
    {
        var copy = element switch
        {
            UiModifier modifier => modifier with { Child = MeasurementCopy(modifier.Child) },
            UiComponentContainer container => container with { Children = container.Children.Select(MeasurementCopy).ToArray() },
            _ => element
        };
        return copy with { Key = "measure-" + element.Key, Events = [] };
    }
}

