using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace Ziopuzzle.CustomButton;

internal static class EditorValidation
{
    // Structure is required for editing. A binding may only become renderable with live data.
    public static string? Check(string xml, IReadOnlyDictionary<string, JsonElement> values)
    {
        var renderer = new LayoutRenderer(xml);
        try { _ = renderer.Render(values); return null; }
        catch (FormatException error)
        {
            var root = XElement.Parse(xml, LoadOptions.SetLineInfo);
            var node = root.DescendantsAndSelf().FirstOrDefault(n =>
                ((IXmlLineInfo)n).LineNumber == (error.Data["LayoutLine"] as int?) &&
                ((IXmlLineInfo)n).LinePosition == (error.Data["LayoutColumn"] as int?));
            if (node == null) throw;
            if (error.Data["LayoutAttributeValue"] is string attributeValue)
            {
                // An unrelated text binding cannot make an invalid literal property valid.
                // Use the renderer's effective value, including active styles and Repeat scope.
                if (!attributeValue.Contains("{{")) throw;
            }
            else if (!node.Attributes().Any(a => a.Value.Contains("{{")) &&
                !node.Nodes().OfType<XText>().Any(t => t.Value.Contains("{{")) &&
                !node.Elements("style").Attributes().Any(a => a.Value.Contains("{{"))) throw;
            return "Layout accepted, but cannot preview with the current data: " + error.Message +
                " Check the binding and live data.";
        }
    }
}
