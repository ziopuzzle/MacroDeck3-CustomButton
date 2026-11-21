using System.Text.Json;
using System.Xml.Linq;
using MacroDeck.Ui.Components;
using MacroDeck.Ui.Dsl;
using MacroDeck.Ui.Model.Surfaces;
using MacroDeck.Ui.Runtime;

namespace Ziopuzzle.CustomButton;

public sealed partial class LayoutRenderer
{
    internal void ValidateDraft(IReadOnlyDictionary<string, JsonElement> values, bool modal = false,
        Func<string, int, IReadOnlyList<double>>? history = null)
    {
        previewedNodes = [];
        draftBindingError = null;
        try
        {
            CheckLayoutLiterals();
            var content = Render(values, history, input: _ => { }, modal: modal);
            // An empty Repeat or a hidden ancestor has no rendered descendants. Check
            // their components individually without inventing item data or emitting nodes.
            var probe = new RenderContext(null, null, _ => { }, null, null, null, modal, null, InspectTemplate: true);
            foreach (var node in root.DescendantsAndSelf().Where(n => n.Name != "style" && !previewedNodes.Contains(n)))
            {
                try { _ = new ComponentRenderer(this, node, values, false, probe, null).Render(); }
                catch (FormatException e)
                {
                    if (e.Data["LayoutAttributeValue"] is string raw && !raw.Contains("{{") &&
                        e.Data["LayoutAttribute"] is string name && !node.Elements("style").Attributes(name).Any())
                        throw LocatedError(node, e);
                    // An unavailable binding or a relationship may depend on a real item.
                }
            }
            if (draftBindingError != null) throw draftBindingError;
            var surface = new UiSurface { Kind = modal ? UiSurfaceKinds.Dialog : UiSurfaceKinds.Widget, SessionMode = "shared" };
            using var view = new UiView(surface, SurfaceRoot(content, modal, HasRootBackground(values), []));
            _ = LayoutLimits.Validate(view.Tree.Root);
        }
        catch (Exception e) when (e is UiViewException or JsonException or ArgumentException)
        { throw new FormatException(e.Message, e); }
        finally { previewedNodes = null; draftBindingError = null; }
    }

    private void CheckLayoutLiterals()
    {
        foreach (var element in root.DescendantsAndSelf())
        {
            var component = element.Name == "style" ? element.Parent! : element;
            foreach (var field in element.Attributes())
            {
                if (field.Value.Contains("{{")) continue;
                var name = field.Name.LocalName;
                var raw = field.Value.Trim();
                try
                {
                    switch (name)
                    {
                        case "fill" or "visible" or "disabled" or "interactive" or "closeModal" or "showCursorWhileTouching" or "wrap" or "shadow" or "seconds":
                            ComponentRenderer.ParseBoolean(name, raw, false);
                            break;
                        case "padding" or "gap" or "mainSize" or "width" or "height" or "x" or "y" or "cornerRadius":
                        case "minWidth" or "maxWidth" or "minHeight" or "maxHeight" or "radius" when component.Name == "modifier":
                            ComponentRenderer.ParseLength(name, raw, 0, 4);
                            break;
                        case "x1" or "x2" or "y1" or "y2" or "cx" or "cy" or "gradientX" or "gradientY" or "plotTop":
                        case "radius" when component.Name == "sector":
                            ComponentRenderer.ParseLength(name, raw, 0, 1);
                            break;
                        case "clip" or "direction" or "justify" or "align" or "coordinates" or "fit" or "frame" or "corner":
                            ComponentRenderer.ParseChoice(component.Name.LocalName, name, raw);
                            break;
                    }
                }
                catch (FormatException error)
                {
                    error.Data["LayoutAttribute"] = name;
                    error.Data["LayoutAttributeValue"] = field.Value;
                    throw LocatedError(element, error);
                }
            }
        }
    }

    internal static UiElement SurfaceRoot(UiElement content, bool modal, bool hasRootBackground, IReadOnlyList<UiEventHandler> events)
        => modal
            ? new UiStack { Key = "root", Fill = true, Padding = 0, Gap = 0, Background = "transparent", Children =
                [new UiModifier { Key = "corners", Radius = 0, Child = new UiButton {
                    Key = "contentSurface", Fill = true, Padding = 0, Gap = 0, Background = "transparent", Children = [content] } }] }
            : new UiButton { Key = "root", Fill = true, Padding = 0, Gap = 0,
                Background = hasRootBackground ? "transparent" : "#1a1a1a", Children = [content], Events = events };
}
