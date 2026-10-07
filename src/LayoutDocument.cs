using System.Xml.Linq;
using System.Xml;
using System.Text;

namespace Ziopuzzle.CustomButton;

// Editing operates on a private copy. Only a completely validated edit becomes the saved XML.
public sealed class LayoutDocument
{
    public XElement Root { get; private set; }
    public LayoutDocument(string xml)
    {
        _ = new LayoutRenderer(xml); // Includes DTD prohibition, limits, attributes and unique IDs.
        Root = XElement.Parse(xml, LoadOptions.PreserveWhitespace);
    }
    public IEnumerable<XElement> Components => Root.DescendantsAndSelf().Where(e => e.Name != "style");
    public XElement Find(string id) => Components.SingleOrDefault(e => (string?)e.Attribute("id") == id)
        ?? throw new FormatException("The selected component was not found.");
    public static bool IsContainer(XElement element) => element.Name.LocalName is "stack" or "layer" or "transform" or "modifier" or "responsive" or "variant" or "segmented";
    public string Serialize()
    {
        // All non-text components can contain styles with insignificant indentation.
        // Preserve text and SVG content, including whitespace-only labels and CDATA.
        var formatted = new XElement(Root);
        foreach (var container in formatted.DescendantsAndSelf().Where(e => e.Name.LocalName is not ("text" or "svg")))
            container.Nodes().OfType<XText>().Where(t => string.IsNullOrWhiteSpace(t.Value)).Remove();
        var output = new StringBuilder();
        using (var writer = XmlWriter.Create(output, new XmlWriterSettings
        {
            OmitXmlDeclaration = true, Indent = true, IndentChars = "  ",
            NewLineChars = "\n", NewLineHandling = NewLineHandling.Entitize
        })) formatted.WriteTo(writer);
        var xml = output.ToString();
        _ = new LayoutRenderer(xml);
        return xml;
    }
    private string NewId(string type)
    {
        for (var i = 1; ; i++)
        {
            var candidate = type + i;
            if (!Components.Any(n => (string?)n.Attribute("id") == candidate)) return candidate;
        }
    }
    public string Add(string selected, string type)
    {
        _ = LayoutRenderer.Allowed(type);
        var target = Find(selected);
        var child = new XElement(type, new XAttribute("id", NewId(type)));
        switch (type)
        {
            case "segmented":
                child.SetAttributeValue("key", "mode"); child.SetAttributeValue("mainSize", "20%");
                child.Add(new XElement("text", new XAttribute("id", NewId("text")), "A"));
                break;
            case "responsive": child.Add(new XElement("variant", new XAttribute("id", NewId("variant")))); break;
            case "variant": child.SetAttributeValue("minAspect", "1.5"); break;
            case "modifier": child.SetAttributeValue("clip", "bounds"); break;
            case "dynamic-text": child.SetAttributeValue("format", "time"); child.SetAttributeValue("seconds", "true"); break;
            case "icon": child.SetAttributeValue("name", "star"); child.SetAttributeValue("size", "20%"); break;
            case "gauge": child.SetAttributeValue("value", "{{value}}"); break;
            case "transform": child.SetAttributeValue("rotation", "0"); break;
            case "circle": case "capsule": child.SetAttributeValue("width", "40%"); child.SetAttributeValue("height", type == "circle" ? "40%" : "20%"); child.SetAttributeValue("color", "#54dfcc"); break;
            case "path": child.SetAttributeValue("data", "M0 0 L1 0.5 L0 1 Z"); child.SetAttributeValue("width", "40%"); child.SetAttributeValue("height", "40%"); child.SetAttributeValue("color", "#54dfcc"); break;
            case "image": child.SetAttributeValue("source", "{{imageUrl}}"); child.SetAttributeValue("size", "100%"); break;
            case "svg": child.Add(new XCData("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"200\" height=\"200\" viewBox=\"0 0 200 200\"><circle cx=\"100\" cy=\"100\" r=\"80\" fill=\"#54dfcc\"/></svg>")); child.SetAttributeValue("size", "100%"); break;
            case "text": child.Value = "Text"; child.SetAttributeValue("size", "18%"); break;
            case "polygon": child.SetAttributeValue("points", "10%,90%;50%,10%;90%,90%"); child.SetAttributeValue("color", "#54dfcc"); break;
            case "sector": child.SetAttributeValue("radius", "40%"); child.SetAttributeValue("startAngle", "-90"); child.SetAttributeValue("sweepAngle", "120"); child.SetAttributeValue("color", "#54dfcc"); break;
            case "stack": child.SetAttributeValue("direction", "vertical"); break;
            case "bar": child.SetAttributeValue("value", "{{value}}"); break;
            case "trackpad": child.SetAttributeValue("keyX", "x"); child.SetAttributeValue("keyY", "y"); break;
            case "dial": case "toggle": case "slider": child.SetAttributeValue("key", "value"); break;
            case "chart": child.SetAttributeValue("key", "value"); break;
            case "clock": child.SetAttributeValue("seconds", "true"); break;
            case "rect": child.SetAttributeValue("width", "40%"); child.SetAttributeValue("height", "20%"); child.SetAttributeValue("color", "#54dfcc"); break;
            case "line": child.SetAttributeValue("x1", "10%"); child.SetAttributeValue("y1", "80%"); child.SetAttributeValue("x2", "90%"); child.SetAttributeValue("y2", "20%"); child.SetAttributeValue("thickness", "2%"); child.SetAttributeValue("color", "#54dfcc"); break;
            case "progress-bar": child.SetAttributeValue("positionMs", "{{positionMs}}"); child.SetAttributeValue("durationMs", "100000"); break;
        }
        if (IsContainer(target)) target.Add(child);
        else if (target.Parent != null) target.AddAfterSelf(child);
        else throw new FormatException("The root is a leaf component. Use Wrap in stack first.");
        return (string)child.Attribute("id")!;
    }
    public string Duplicate(string selected)
    {
        var original = Find(selected);
        if (original.Parent == null) throw new FormatException("The root cannot be duplicated. Wrap it in a stack first.");
        var clone = new XElement(original);
        original.AddAfterSelf(clone);
        foreach (var node in clone.DescendantsAndSelf().Where(n => n.Name != "style")) node.SetAttributeValue("id", NewId(node.Name.LocalName));
        return (string)clone.Attribute("id")!;
    }
    public string Delete(string selected)
    {
        var node = Find(selected);
        if (node.Parent == null) throw new FormatException("The root cannot be deleted.");
        var parent = (string)node.Parent.Attribute("id")!;
        node.Remove(); return parent;
    }
    public string Wrap(string selected, string container = "stack")
    {
        if (container is not ("stack" or "layer")) throw new FormatException("Wrap in stack or layer.");
        var node = Find(selected);
        var wrapper = new XElement(container, new XAttribute("id", NewId(container)));
        if (container == "stack") wrapper.SetAttributeValue("direction", "vertical");
        if (node.Parent == null) { Root = wrapper; wrapper.Add(node); }
        else { node.ReplaceWith(wrapper); wrapper.Add(node); }
        return (string)wrapper.Attribute("id")!;
    }
    public void Reorder(string selected, int direction)
    {
        var node = Find(selected);
        var sibling = direction < 0 ? node.ElementsBeforeSelf().LastOrDefault(n => n.Name != "style") : node.ElementsAfterSelf().FirstOrDefault(n => n.Name != "style");
        if (sibling == null) return;
        node.Remove();
        if (direction < 0) sibling.AddBeforeSelf(node); else sibling.AddAfterSelf(node);
    }
    public void Move(string selected, string parentId)
    {
        var node = Find(selected); var parent = Find(parentId);
        if (!IsContainer(parent) || node.Parent == null || parent.AncestorsAndSelf().Contains(node))
            throw new FormatException("Cannot move into itself, its descendants or a leaf component.");
        node.Remove(); parent.Add(node);
    }
    public void Place(string selected, string targetId, string placement)
    {
        if (placement == "inside") { Move(selected, targetId); return; }
        if (placement is not ("before" or "after")) throw new FormatException("Invalid drop position.");
        var node = Find(selected); var target = Find(targetId);
        if (node == target) return;
        if (node.Parent == null || target.Parent == null || target.AncestorsAndSelf().Contains(node))
            throw new FormatException("Cannot move outside the root or into a descendant.");
        node.Remove();
        if (placement == "before") target.AddBeforeSelf(node); else target.AddAfterSelf(node);
    }
    public void SetText(string selected, string text)
    {
        var node = Find(selected);
        if (node.Name != "text") throw new FormatException("Only text components can contain text.");
        node.Nodes().OfType<XText>().Remove();
        node.AddFirst(new XText(text)); // Conditions and comments remain intact.
    }
    public void SetAttribute(string selected, string name, string value)
        => Find(selected).SetAttributeValue(name, string.IsNullOrEmpty(value) ? null : value);
    public void AddStyle(string selected) => Find(selected).Add(new XElement("style", new XAttribute("when", "value >= 80")));
    public void SetStyle(string selected, int index, string attribute, string value)
        => Find(selected).Elements("style").ElementAt(index).SetAttributeValue(attribute, string.IsNullOrEmpty(value) && attribute != "when" ? null : value);
    public void DeleteStyle(string selected, int index) => Find(selected).Elements("style").ElementAt(index).Remove();
}
