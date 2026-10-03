using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using MacroDeck.Ui.Model.Nodes;

namespace Ziopuzzle.CustomButton;

public static class LayoutLimits
{
    public const int XmlCharacters = 65536;
    public const int XmlElements = 512;
    public const int XmlDepth = 32;
    // beta.15 allows 2,000 nodes, JSON depth 32, and 64 KiB per patch.
    // Reserve room for the session envelope and replace-node operation.
    public const int RenderNodes = 2000;
    public const int RenderBytes = 56 * 1024;
    public const int RenderJsonDepth = 26;
    public const int PatchBytes = 60 * 1024;

    public static string Validate(UiNode root)
    {
        var pending = new Stack<UiNode>(); pending.Push(root);
        var count = 0;
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (++count > RenderNodes) throw new FormatException($"Rendered layout exceeds {RenderNodes} nodes. Simplify components or responsive branches.");
            foreach (var child in node.Children) pending.Push(child);
            if (node.Fallback != null) pending.Push(node.Fallback);
        }
        var json = JsonSerializer.Serialize(root);
        var bytes = Encoding.UTF8.GetBytes(json);
        if (bytes.Length > RenderBytes)
            throw new FormatException($"Rendered layout uses {bytes.Length} bytes; the limit is {RenderBytes} bytes to allow live updates. Reduce text, paths or components.");
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = 128 });
        while (reader.Read())
            if (reader.CurrentDepth >= RenderJsonDepth)
                throw new FormatException($"Rendered layout exceeds the safe JSON depth of {RenderJsonDepth}. Reduce nested containers or effects; XML levels and rendered JSON depth differ.");
        return json;
    }
}
