using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Ziopuzzle.CustomButton;

/// <summary>A bounded, self-contained SVG document. Bind values through XML nodes, never raw markup.</summary>
public static class SvgTemplate
{
    public const string Prefix = "svg-inline:";
    public const int MaxCharacters = 32768;
    private const string Elements = "svg g defs title desc rect circle ellipse line polyline polygon path text tspan linearGradient radialGradient stop clipPath";
    private const string Attributes = "id viewBox width height x y x1 y1 x2 y2 cx cy r rx ry d points fill fill-opacity fill-rule stroke stroke-width stroke-opacity stroke-linecap stroke-linejoin stroke-miterlimit stroke-dasharray stroke-dashoffset opacity transform font-family font-size font-weight font-style text-anchor dominant-baseline dx dy offset stop-color stop-opacity gradientUnits gradientTransform spreadMethod fx fy clip-path clip-rule clipPathUnits preserveAspectRatio";
    public static string Source(string template, Func<string, string> expand, int pixels)
    {
        var root = Parse(template);
        foreach (var node in root.DescendantsAndSelf())
        {
            foreach (var attribute in node.Attributes().Where(a => !a.IsNamespaceDeclaration).ToArray())
                attribute.Value = expand(attribute.Value);
            foreach (var text in node.Nodes().OfType<XText>().ToArray()) text.Value = expand(text.Value);
        }
        var xml = root.ToString(SaveOptions.DisableFormatting);
        _ = Parse(xml); // Validate values after expansion, including local-only paint references.
        return Prefix + pixels.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n" + xml;
    }
    public static XElement Parse(string xml)
    {
        if (xml.Length > MaxCharacters) throw new FormatException("SVG must not exceed 32,768 characters.");
        XElement root;
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxCharacters });
            root = XElement.Load(reader, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException) { throw new FormatException("Enter a valid SVG document without DTDs."); }
        if (root.Name.LocalName != "svg") throw new FormatException("SVG content must have an svg root element.");
        var nodes = root.DescendantsAndSelf().ToArray();
        if (nodes.Length > 256 || nodes.Any(n => n.Ancestors().Count() >= 16)) throw new FormatException("SVG is limited to 256 elements and 16 levels.");
        foreach (var node in nodes)
        {
            if (node.Name.NamespaceName is not ("" or "http://www.w3.org/2000/svg") || !Elements.Split(' ').Contains(node.Name.LocalName))
                throw new FormatException("Unsupported SVG element. Use self-contained shapes, text, gradients and clipping paths.");
            foreach (var attribute in node.Attributes())
            {
                if (attribute.IsNamespaceDeclaration) continue;
                if (attribute.Name.Namespace != XNamespace.None || !Attributes.Split(' ').Contains(attribute.Name.LocalName))
                    throw new FormatException("Unsupported SVG attribute. Stylesheets, scripts, external references and embedded images are not supported.");
                var value = attribute.Value.Trim();
                if (value.Contains("url", StringComparison.OrdinalIgnoreCase) && !System.Text.RegularExpressions.Regex.IsMatch(value, @"^url\(#[A-Za-z_][A-Za-z0-9_.-]*\)$"))
                    throw new FormatException("SVG paint references must name a local ID: url(#id).");
                if (value.Contains('\\')) throw new FormatException("Escaped SVG attribute values are not supported.");
            }
        }
        return root;
    }
}
