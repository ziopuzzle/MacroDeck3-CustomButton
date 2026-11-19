using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Ziopuzzle.CustomButton;

public sealed partial class LayoutRenderer
{
    internal static string Allowed(string type) => "id fill mainSize visible visibleWhen opacity transitionMs transitionProperties easing colorSpace " + (type switch
    {
        "repeat" => "items itemKey itemAlias indexAlias keyAlias direction justify align gap padding background",
        "scroll" => "direction anchor followKey followOffset followDelayMs gap padding background",
        "stack" => "direction justify align gap padding background borderStyle borderColor interactive closeModal result",
        "layer" => "interactive closeModal result background",
        "transform" => "rotation originX originY zoom offsetX offsetY",
        "modifier" => "padding clip radius width height minWidth maxWidth minHeight maxHeight disabled",
        "responsive" => "",
        "variant" => "minWidth maxWidth minHeight maxHeight minAspect maxAspect",
        "dynamic-text" => "zone format seconds size sizeCap minSize weight color role align",
        "icon" => "name size color role",
        "gauge" => "value min max startAngle endAngle color trackColor thickness",
        "image" => "source size frame fit zoom offsetX offsetY transition brightness saturation color averageColorKey averageColorFallback",
        "svg" => "rasterSize size frame fit zoom offsetX offsetY transition brightness saturation color averageColorKey averageColorFallback",
        "text" => "size sizeCap minSize weight fontFace color role digits align wrap maxLines shadow strokeColor strokeWidth",
        "bar" => "value start marker min max color endColor thickness",
        "chart" => "key min max points color plotTop thickness",
        "clock" => "zone seconds color",
        "progress-bar" => "positionMs durationMs anchor rate color endColor thickness",
        "slider" => "value key fallback min max step direction color thickness interactive",
        "dial" => "value key fallback min max step startAngle endAngle color thickness interactive",
        "toggle" => "value key size color interactive",
        "text-field" => "value key placeholder size interactive",
        "segmented" => "value key color interactive",
        "trackpad" => "keyX keyY leftValue rightValue topValue bottomValue stepX stepY color background interactive showCursorWhileTouching",
        "rect" => "coordinates x y width height color corner cornerRadius strokeColor strokeWidth gradient endColor gradientAngle gradientX gradientY",
        "circle" or "capsule" => "coordinates x y width height color strokeColor strokeWidth gradient endColor gradientAngle gradientX gradientY",
        "path" => "coordinates x y width height data color strokeColor strokeWidth",
        "line" => "coordinates x y length thickness direction color x1 y1 x2 y2 cx cy angle gradient endColor gradientAngle gradientX gradientY",
        "polygon" => "coordinates points color strokeColor strokeWidth",
        "sector" => "coordinates cx cy radius startAngle sweepAngle color strokeColor strokeWidth",
        _ => throw new FormatException($"Unsupported component: {type}")
    });

    public LayoutRenderer(string layout)
    {
        if (layout.Length > LayoutLimits.XmlCharacters) throw new FormatException($"Layouts must not exceed {LayoutLimits.XmlCharacters} characters.");
        using var reader = XmlReader.Create(new StringReader(layout), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = LayoutLimits.XmlCharacters });
        root = XElement.Load(reader, LoadOptions.SetLineInfo);
        var nodes = root.DescendantsAndSelf().ToArray();
        if (nodes.Length > LayoutLimits.XmlElements) throw new FormatException($"Layouts must not exceed {LayoutLimits.XmlElements} XML elements, including styles.");
        if (nodes.Any(e => e.Ancestors().Count() >= LayoutLimits.XmlDepth)) throw new FormatException($"Layouts must not exceed {LayoutLimits.XmlDepth} XML levels, including the root.");
        var ids = new Dictionary<string, XElement>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            try
            {
                if (node.Name.Namespace != XNamespace.None) throw new FormatException("XML namespaces are not supported.");
                if (node.Name.LocalName == "style")
                {
                    if (node.Attributes().Any(a => a.Name.LocalName is "itemAlias" or "indexAlias" or "keyAlias"))
                        throw new FormatException("Set Repeat aliases on Repeat itself, not on a style.");
                    if (node.Attribute("averageColorKey") != null || node.Attribute("averageColorFallback") != null)
                        throw new FormatException("Set averageColorKey and averageColorFallback on the image/SVG itself, not on a style.");
                    if (node.Parent == null || node.Parent.Name.LocalName == "style" || node.HasElements || !string.IsNullOrWhiteSpace(node.Value)) throw new FormatException("Place style directly inside a component and leave it empty.");
                    var condition = (string?)node.Attribute("when") ?? throw new FormatException("A style needs a when condition.");
                    DisplayCondition.Evaluate(condition, new Dictionary<string, JsonElement>());
                    foreach (var attribute in node.Attributes())
                        if (attribute.Name != "when" && (attribute.Name == "id" || !Allowed(node.Parent.Name.LocalName).Split(' ').Contains(attribute.Name.ToString()))) throw new FormatException("Unsupported style attribute: " + attribute.Name);
                    continue;
                }
                var id = (string?)node.Attribute("id") ?? throw new FormatException("Each component needs an id.");
                if (!Regex.IsMatch(id, "^[A-Za-z][A-Za-z0-9_-]{0,31}$")) throw new FormatException($"Invalid ID '{id}': IDs must begin with a letter and contain at most 32 letters, digits, underscores or hyphens.");
                if (ids.TryGetValue(id, out var first)) throw new FormatException($"Duplicate ID '{id}'. First defined on <{first.Name}> at line {((IXmlLineInfo)first).LineNumber}, column {((IXmlLineInfo)first).LinePosition}.");
                ids.Add(id, node);
                var allowed = Allowed(node.Name.LocalName);
                if (node.Name == "repeat")
                {
                    _ = RepeatAliases.Read(node);
                    ValidateRepeatSource((string?)node.Attribute("items") ?? "");
                    var itemKey = (string?)node.Attribute("itemKey") ?? "";
                    if (itemKey.Length > 0 && !Regex.IsMatch(itemKey, @"\A[A-Za-z0-9_.-]+\z"))
                        throw new FormatException("itemKey must be a literal item property path, such as id.");
                }
                if (node.Attribute("averageColorKey") is { } output && output.Value.Length > 0)
                {
                    if (node.Ancestors().Any(a => a.Name == "repeat"))
                        throw new FormatException("Image average-color output is not supported inside repeat; each output needs a unique literal data name.");
                    DataHub.ValidateName(output.Value);
                    var fallback = DisplayColor.Parse((string?)node.Attribute("averageColorFallback") ?? "transparent").Hex;
                    if (!ImageColorDefaults.TryAdd(output.Value, JsonSerializer.SerializeToElement(fallback)))
                        throw new FormatException("Image average-color output names must be unique within a layout.");
                }
                foreach (var attribute in node.Attributes())
                    if (!allowed.Split(' ').Contains(attribute.Name.ToString())) throw new FormatException($"Unsupported attribute on {node.Name}: {attribute.Name}");
                if (node.Attribute("visibleWhen") is { } conditionAttribute) DisplayCondition.Evaluate(conditionAttribute.Value, new Dictionary<string, JsonElement>());
                if (!LayoutDocument.IsContainer(node) && node.Elements().Any(e => e.Name != "style")) throw new FormatException($"{node.Name} cannot contain child components.");
                if (node.Name == "responsive" && node.Elements().Any(e => e.Name != "variant" && e.Name != "style")) throw new FormatException("Responsive children must be variant components; the first is the default layout.");
                if (node.Name == "variant" && node.Parent?.Name != "responsive") throw new FormatException("Place variant directly inside responsive.");
                if (node.Name.LocalName is not ("text" or "svg") && node.Nodes().OfType<XText>().Any(t => !string.IsNullOrWhiteSpace(t.Value))) throw new FormatException("Place text inside a text or SVG component.");
            }
            catch (FormatException e) { throw LocatedError(node, e); }
        }
    }
    private static FormatException LocatedError(XElement node, Exception error)
    {
        var location = (IXmlLineInfo)node;
        var owner = node.Name == "style" ? node.Parent : node;
        var result = new FormatException($"<{node.Name}> (ID '{owner?.Attribute("id")?.Value ?? "missing"}'): {error.Message} (line {location.LineNumber}, column {location.LinePosition})", error);
        result.Data["LayoutLine"] = location.LineNumber; result.Data["LayoutColumn"] = location.LinePosition;
        if (error.Data.Contains("LayoutAttribute"))
        {
            result.Data["LayoutAttribute"] = error.Data["LayoutAttribute"];
            result.Data["LayoutAttributeValue"] = error.Data["LayoutAttributeValue"];
        }
        return result;
    }
}
