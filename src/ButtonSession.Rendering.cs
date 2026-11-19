using System.Text.Json;
using MacroDeck.Ui.Components;
using MacroDeck.Ui.Dsl;
using MacroDeck.Ui.Model.Nodes;
using MacroDeck.Ui.Model.Surfaces;
using MacroDeck.Ui.Runtime;

namespace Ziopuzzle.CustomButton;

public sealed partial class ButtonSession
{
    private readonly Dictionary<string, string> publishedImageColors = new(StringComparer.Ordinal);

    private (UiTree Tree, string Json) BuildCurrent(DataSnapshot snapshot, string? selectedId)
    {
        try
        {
            if (configurationError != null) throw new FormatException(configurationError);
            if (pressError != null) throw new FormatException(pressError);
            if (updateError != null) throw new FormatException(updateError);
            var values = ReadDisplayValues(snapshot);
            var content = RenderContent(values, snapshot, selectedId);
            var result = BuildView(content, renderer?.HasRootBackground(values) ?? true);
            PublishImageColors(snapshot);
            lastRenderFailure = null;
            reportError?.Invoke(null);
            return result;
        }
        catch (Exception e) when (e is FormatException or ArgumentException or UiViewException or JsonException)
        {
            if (lastRenderFailure != e.Message) Trace("render error; animation reset: " + e.Message);
            lastRenderFailure = e.Message;
            reportError?.Invoke(e.Message);
            animation.Reset();
            return BuildView(new UiStack { Key = "error", Fill = true, Padding = .06, Background = "#401f28", Children =
            [ new UiTextRun { Key = "errorTitle", Shadow = false, Text = "Custom Button", Size = .12, Color = "#ffb8c3" },
              new UiTextRun { Key = "message", Shadow = false, Text = TextCatalog.Reference(e.Message), Size = .12, MinSize = .06, Wrap = true, MaxLines = 8, Color = "#ffb8c3" } ] });
        }
    }

    private Dictionary<string, JsonElement> ReadDisplayValues(DataSnapshot snapshot)
    {
        var values = new Dictionary<string, JsonElement>(initial, StringComparer.Ordinal);
        foreach (var pair in snapshot.Values) values[pair.Key] = pair.Value;
        if (renderer != null)
            foreach (var pair in renderer.ImageColorDefaults)
                if (publishImageColors) values.TryAdd(pair.Key, pair.Value); else values[pair.Key] = pair.Value;
        if (images != null)
            foreach (var pair in publishImageColors ? images.CompletedColors() : images.Colors()) values[pair.Key] = JsonSerializer.SerializeToElement(pair.Value);
        return values;
    }

    private UiElement RenderContent(IReadOnlyDictionary<string, JsonElement> values, DataSnapshot snapshot, string? selectedId)
    {
        images?.BeginRender();
        var imageRatios = new Dictionary<string, double>();
        MacroDeck.Ui.Model.Resources.UiResource? ResolveImage(string id, string source)
        {
            var image = images!.ResolveImage(id, source);
            imageRatios[id] = image.AspectRatio;
            return image.Resource;
        }
        return renderer != null ? renderer.Render(values, snapshot.ReadHistory, selectedId, onControlInput != null ? HandleControlInput : null,
            animation, images == null ? null : ResolveImage, id => imageRatios.GetValueOrDefault(id, 1), modal: surface.Kind == UiSurfaceKinds.Dialog, modalAnswer: modalAnswer) : settings.Design!.Render(values);
    }

    private void PublishImageColors(DataSnapshot snapshot)
    {
        if (publishImageColors && hub != null && renderer != null && images != null)
        {
            var outputs = new Dictionary<string, JsonElement>();
            foreach (var pair in renderer.ImageColorDefaults)
                if (!snapshot.Values.ContainsKey(pair.Key)) outputs[pair.Key] = pair.Value;
            var completedColors = images.CompletedColors();
            foreach (var pair in completedColors)
                if ((!publishedImageColors.TryGetValue(pair.Key, out var published) || published != pair.Value || !snapshot.Values.ContainsKey(pair.Key))
                    && (!snapshot.Values.TryGetValue(pair.Key, out var current) || current.ValueKind != JsonValueKind.String || current.GetString() != pair.Value))
                    outputs[pair.Key] = JsonSerializer.SerializeToElement(pair.Value);
            if (outputs.Count > 0) hub.Update(settings.Channel, outputs);
            foreach (var pair in completedColors) publishedImageColors[pair.Key] = pair.Value;
        }
    }

    private (UiTree Tree, string Json) BuildView(UiElement content, bool hasRootBackground = false)
    {
        // A stable native button supplies the tile clip and text-shadow context.
        // Explicit transparency prevents the host's default accent background.
        currentView = new UiView(surface, surface.Kind == UiSurfaceKinds.Dialog
            ? new UiStack { Key = "root", Fill = true, Padding = 0, Gap = 0, Background = "transparent", Children =
                // Radius modifiers are ignored on the tree root. Apply one directly to
                // a nested, noninteractive button to supply shadows without tile corners.
                [new UiModifier { Key = "corners", Radius = 0, Child = new UiButton {
                    Key = "contentSurface", Fill = true, Padding = 0, Gap = 0,
                    Background = "transparent", Children = [content] } }] }
            : new UiButton { Key = "root", Fill = true, Padding = 0, Gap = 0,
            Background = hasRootBackground ? "transparent" : "#1a1a1a", Children = [content],
            Events = !settings.WholeButtonInteraction ? [] : onWidgetEvent != null
                ? new[] { UiComponentEvents.Press, UiComponentEvents.LongPress, UiComponentEvents.PressStart, UiComponentEvents.PressEnd }
                    .Select(name => UiEventHandler.On(name, () => { })).ToArray()
                : press == null ? [] : [UiEventHandler.On(UiComponentEvents.Press, () => { })] });
        return (currentView.Tree, LayoutLimits.Validate(currentView.Tree.Root));
    }
}
