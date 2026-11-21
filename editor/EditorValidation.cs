using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace Ziopuzzle.CustomButton;

internal static class EditorValidation
{
    // Structure is required for editing. A binding may only become renderable with live data.
    internal sealed record Issue(string Message, int Line, int Column, int Length, string? Id = null,
        string? Field = null, string? RevertedXml = null, int? Condition = null)
    {
        public string Location => string.Join(" · ", new[] { Id, Condition is int n ? $"Condition {n}" : null, Field }.Where(s => !string.IsNullOrEmpty(s)));
    }

    public static Issue Describe(Exception error)
    {
        var field = error.Data["LayoutField"] as string;
        var line = error.Data["LayoutFieldLine"] as int? ?? error.Data["LayoutLine"] as int? ?? (error as XmlException)?.LineNumber ?? 0;
        var column = error.Data["LayoutFieldColumn"] as int? ?? error.Data["LayoutColumn"] as int? ?? (error as XmlException)?.LinePosition ?? 0;
        return new(error.Message, line, column, field?.Length ?? 1, error.Data["LayoutId"] as string,
            field, Condition: error.Data["LayoutCondition"] as int?);
    }

    public static Issue? Check(string xml, IReadOnlyDictionary<string, JsonElement> values, string? lastPreviewable = null, bool modal = false)
    {
        var renderer = new LayoutRenderer(xml);
        try { renderer.ValidateDraft(values, modal); return null; }
        catch (FormatException error)
        {
            if (error.Data["LayoutDataDependent"] is not true) throw;
            var root = XElement.Parse(xml, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
            var node = root.DescendantsAndSelf().FirstOrDefault(n =>
                ((IXmlLineInfo)n).LineNumber == (error.Data["LayoutLine"] as int?) &&
                ((IXmlLineInfo)n).LinePosition == (error.Data["LayoutColumn"] as int?));
            if (node == null) throw;
            var owner = node.Name == "style" ? node.Parent! : node;
            var issue = Describe(error);
            var style = issue.Condition is int index ? owner.Elements("style").ElementAt(index - 1) : null;
            var field = issue.Field == null ? null : (style ?? owner).Attribute(issue.Field);
            var reverted = field == null || lastPreviewable == null ? null : RevertField(xml, owner, style, field, lastPreviewable);
            if (reverted != null)
            {
                try { _ = Check(reverted, values, modal: modal); }
                catch (Exception e) when (e is FormatException or XmlException or InvalidOperationException) { reverted = null; }
            }
            return issue with { Message = $"{issue.Location} — Layout accepted, but cannot preview with the current data: {error.Message}",
                Length = field?.Name.LocalName.Length ?? node.Name.LocalName.Length, RevertedXml = reverted };
        }
    }

    internal static int Offset(string text, int line, int column)
    {
        var offset = 0;
        for (var i = 1; i < line && offset < text.Length; i++)
        {
            while (offset < text.Length && text[offset] is not ('\r' or '\n')) offset++;
            if (offset < text.Length && text[offset] == '\r') offset++;
            if (offset < text.Length && text[offset] == '\n') offset++;
        }
        return Math.Min(text.Length, offset + Math.Max(0, column - 1));
    }

    private static string? RevertField(string xml, XElement owner, XElement? style, XAttribute field, string previousXml)
    {
        var previousRoot = XElement.Parse(previousXml, LoadOptions.PreserveWhitespace);
        var previous = previousRoot.DescendantsAndSelf().FirstOrDefault(n => n.Name == owner.Name &&
            (string?)n.Attribute("id") == (string?)owner.Attribute("id"));
        if (previous == null) return null;
        if (style != null)
        {
            var matches = previous.Elements("style").Where(s => (string?)s.Attribute("when") == (string?)style.Attribute("when")).ToArray();
            if (matches.Length != 1 || owner.Elements("style").Count(s => (string?)s.Attribute("when") == (string?)style.Attribute("when")) != 1) return null;
            previous = matches[0];
        }
        var oldValue = (string?)previous.Attribute(field.Name);
        if (oldValue == field.Value) return null;
        var location = (IXmlLineInfo)field;
        var start = Offset(xml, location.LineNumber, location.LinePosition);
        var quote = xml.IndexOfAny(['"', '\''], start + field.Name.LocalName.Length);
        var end = xml.IndexOf(xml[quote], quote + 1) + 1;
        if (oldValue != null)
            return xml[..start] + new XAttribute(field.Name, oldValue) + xml[end..];
        while (start > 0 && char.IsWhiteSpace(xml[start - 1])) start--;
        return xml.Remove(start, end - start);
    }
}
