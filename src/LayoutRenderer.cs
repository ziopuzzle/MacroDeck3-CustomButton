using System.Text.Json;
using System.Xml.Linq;
using MacroDeck.Ui.Components;
using MacroDeck.Ui.Dsl;
using MacroDeck.Ui.Model.Resources;

namespace Ziopuzzle.CustomButton;

public sealed partial class LayoutRenderer
{
    public Action<string, string, string>? ConfigureImageColor { get; set; }
    public Dictionary<string, JsonElement> ImageColorDefaults { get; } = new(StringComparer.Ordinal);
    private readonly XElement root;
    internal sealed record PadContact
    {
        public int? Id;
        public double StartX, StartY, PreviousX, PreviousY;
        public double Width, Height;
        public long LastSeen;
    }
    private readonly Dictionary<string, PadContact> padContacts = new(StringComparer.Ordinal);
    internal long InputRevision { get; private set; }
    internal Dictionary<string, PadContact> CopyContacts() => padContacts.ToDictionary(p => p.Key, p => p.Value with { });
    internal void RestoreContacts(Dictionary<string, PadContact>? contacts)
    {
        if (contacts != null) foreach (var pair in contacts) padContacts[pair.Key] = pair.Value with { };
    }
    public bool HasRootBackground(IReadOnlyDictionary<string, JsonElement> values)
        => root.Attribute("background") != null || root.Elements("style").Any(style =>
            style.Attribute("background") != null && DisplayCondition.Evaluate((string)style.Attribute("when")!, values));

    public UiElement Render(IReadOnlyDictionary<string, JsonElement> values, Func<string, int, IReadOnlyList<double>>? history = null, string? selectedId = null, Action<ControlInput>? input = null, DisplayAnimation? animation = null, Func<string, string, UiResource?>? images = null, Func<string, double>? imageAspectRatio = null, bool modal = false, Func<string, string, string>? modalAnswer = null)
    {
        if (ImageColorDefaults.Keys.Any(key => !values.ContainsKey(key)))
        {
            var combined = new Dictionary<string, JsonElement>(ImageColorDefaults, StringComparer.Ordinal);
            foreach (var pair in values) combined[pair.Key] = pair.Value;
            values = combined;
        }
        animation?.BeginFrame();
        expandedComponents = 0;
        scrollLimits.Clear();
        seenScrolls.Clear();
        try { return Make(root, values, false, new RenderContext(history, selectedId, input, animation, images, imageAspectRatio, modal, modalAnswer)) ?? new UiStack { Key = "hidden", Fill = true, Children = [] }; }
        finally
        {
            animation?.EndFrame();
            foreach (var id in scrollFollowing.Keys.Where(id => !seenScrolls.Contains(id)).ToArray()) scrollFollowing.Remove(id);
        }
    }
    private UiElement? Make(XElement node, IReadOnlyDictionary<string, JsonElement> values, bool parentHorizontal, RenderContext context, RepeatScope? scope = null)
    {
        try
        {
            if (++expandedComponents > LayoutLimits.RenderNodes)
                throw new FormatException($"Expanded layout exceeds {LayoutLimits.RenderNodes} components. Reduce the array or repeated template.");
            var element = new ComponentRenderer(this, node, values, parentHorizontal, context, scope).Render();
            return element != null && (string?)node.Attribute("id") == context.SelectedId ? PreviewSelection.Highlight(element) : element;
        }
        catch (FormatException e) when (!e.Data.Contains("LayoutLine")) { throw LocatedError(node, e); }
    }

    private sealed record RenderContext(
        Func<string, int, IReadOnlyList<double>>? History,
        string? SelectedId,
        Action<ControlInput>? Input,
        DisplayAnimation? Animation,
        Func<string, string, UiResource?>? Images,
        Func<string, double>? ImageAspectRatio,
        bool Modal,
        Func<string, string, string>? ModalAnswer);
}
