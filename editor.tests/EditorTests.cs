using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NUnit.Framework;
using System.Xml.Linq;
using Avalonia.Media;

[assembly: AvaloniaTestApplication(typeof(Ziopuzzle.CustomButton.Editor.Tests.TestApp))]
namespace Ziopuzzle.CustomButton.Editor.Tests;

public static class TestApp
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<EditorApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
public class EditorTests
{
    [AvaloniaTest] public void LineModeSwitchRemovesCompetingCoordinates()
    {
        var w = new EditorWindow("<line id='line' x1='10%' y1='50%' x2='90%' y2='50%'/>", "{}", true); w.Show(); Flush(w);
        try
        {
            Find<ComboBox>(w, "lineMode").SelectedIndex = 1; Flush(w);
            var node = XElement.Parse(w.LayoutXml);
            Assert.That(node.Attribute("x1"), Is.Null);
            Assert.That(node.Attribute("angle")!.Value, Is.EqualTo("0"));
            Find<TextBox>(w, "field_angle").Text = "-90"; Flush(w);
            Assert.That(XElement.Parse(w.LayoutXml).Attribute("angle")!.Value, Is.EqualTo("-90"));
            Find<ComboBox>(w, "lineMode").SelectedIndex = 0; Flush(w);
            node = XElement.Parse(w.LayoutXml);
            Assert.That(node.Attribute("angle"), Is.Null);
            Assert.That(node.Attribute("x1")!.Value, Is.EqualTo("10%"));
        }
        finally { w.HostClosed = true; w.Close(); }
    }
    [AvaloniaTest] public void RenameKeepsSelectedElementEditableAndSupportsUndo()
    {
        var w = new EditorWindow("<text id='title'>Hello</text>", "{}", true); w.Show(); Flush(w);
        try
        {
            Find<TextBox>(w, "field_id").Text = "renamed";
            Flush(w);
            Assert.That(XElement.Parse(w.LayoutXml).Attribute("id")!.Value, Is.EqualTo("renamed"));
            Find<TextBox>(w, "field_text").Text = "Updated"; Flush(w);
            Assert.That(XElement.Parse(w.LayoutXml).Value, Is.EqualTo("Updated"));
            Click(w, Find<Button>(w, "undo"));
            Assert.That(XElement.Parse(w.LayoutXml).Attribute("id")!.Value, Is.EqualTo("title"));
            Find<TextBox>(w, "field_id").Text = "";
            Flush(w);
            Assert.That(XElement.Parse(w.LayoutXml).Attribute("id")!.Value, Is.EqualTo("title"));
        }
        finally { w.HostClosed = true; w.Close(); }
    }
    [AvaloniaTest] public void PickerBurstIsCoalescedAndDisconnectKeepsDraftOpen()
    {
        var messages = new List<string>();
        var w = new EditorWindow("<rect id='shape'/>", "{}", true, messages.Add); w.Show(); Flush(w);
        try
        {
            var color = Find<TextBox>(w, "field_color");
            for (var i = 0; i < 80; i++) color.Text = $"#{i:x2}0080";
            Assert.That(messages, Is.Empty, "Input must not publish once per picker event.");
            Thread.Sleep(300); Flush(w);
            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0], Does.Contain("#4f0080"));
            color.Text = "#123456"; w.Disconnect(); Flush(w);
            Assert.That(w.IsVisible, Is.True);
            Assert.That(w.LayoutXml, Does.Contain("#123456"));
            Assert.That(w.Title, Does.Contain("Disconnected"));
            Assert.That(messages, Has.Count.EqualTo(1));
            var propertyScroll = Find<ScrollViewer>(w, "propertiesScroll");
            Assert.That(((Control)propertyScroll.Content!).Margin.Right, Is.GreaterThanOrEqualTo(20));
        }
        finally { w.HostClosed = true; w.Close(); }
    }
    [AvaloniaTest] public void AddMenuGroupsEveryComponentAndAddsEndpointLine()
    {
        var w = new EditorWindow("<stack id='root'/>", "{}", true); w.Show(); Flush(w);
        try
        {
            var add = w.GetVisualDescendants().OfType<Button>().First(b => b.Content?.ToString() == "＋ Add component");
            add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(w);
            var groups = add.ContextMenu!.Items.Cast<MenuItem>().ToArray();
            Assert.That(groups.Select(g => g.Header), Is.EqualTo(new[] { "Layout", "Text and images", "Data and controls", "Shapes" }));
            var entries = groups.SelectMany(g => g.Items.Cast<MenuItem>()).ToArray();
            Assert.That(entries.Length, Is.EqualTo(16));
            Assert.That(entries.Select(e => e.Header).Distinct().Count(), Is.EqualTo(16));
            entries.Single(e => e.Header?.ToString() == "╱ Line").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Flush(w);
            var line = XElement.Parse(w.LayoutXml).Element("line")!;
            Assert.That(line.Attribute("x1")!.Value, Is.EqualTo("10%"));
            Assert.That(line.Attribute("x2")!.Value, Is.EqualTo("90%"));
        }
        finally { w.HostClosed = true; w.Close(); }
    }
    [AvaloniaTest] public void ContextMenuCanWrapRootInLayer()
    {
        var w = new EditorWindow("<text id='title'>Title</text>", "{}", true); w.Show(); Flush(w);
        try
        {
            var more = w.GetVisualDescendants().OfType<Button>().Single(b => b.Content?.ToString() == "⋯");
            more.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(w);
            more.ContextMenu!.Items.Cast<MenuItem>().Single(m => m.Header?.ToString() == "Wrap in layer").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Flush(w);
            Assert.That(XElement.Parse(w.LayoutXml).Name.LocalName, Is.EqualTo("layer"));
            Assert.That(XElement.Parse(w.LayoutXml).Element("text")!.Value, Is.EqualTo("Title"));
        }
        finally { w.HostClosed = true; w.Close(); }
    }
    [AvaloniaTest] public void FontInputPublishesBindingsAndCanRestoreDefault()
    {
        var w = new EditorWindow("<text id='title'>Title</text>", "{\"face\":\"test-face\"}", true); w.Show(); Flush(w);
        try
        {
            var field = Find<TextBox>(w, "field_fontFace");
            field.Text = "{{face}}"; Flush(w);
            Assert.That(XElement.Parse(w.LayoutXml).Attribute("fontFace")?.Value, Is.EqualTo("{{face}}"));
            field.Text = ""; Flush(w);
            Assert.That(XElement.Parse(w.LayoutXml).Attribute("fontFace"), Is.Null);
        }
        finally { w.HostClosed = true; w.Close(); }
    }
    [AvaloniaTest] public void DraftDescriptionsAndColourSwatchUpdateWithoutApplyingOrLosingFocus()
    {
        const string layout = "<rect id='shape' width='40%' color='#123456'><style when='value > 50' color='#ff0000'/></rect>";
        var w = new EditorWindow(layout, "{}", true); w.Show(); Flush(w);
        try
        {
            var width = Find<TextBox>(w, "field_width"); width.Focus(); width.Text = "70%"; Flush(w);
            var caption = Find<TextBlock>(w, "caption_shape");
            Assert.That(caption.Inlines!.OfType<Avalonia.Controls.Documents.Run>().Last().Text, Does.Contain("70%"));
            Assert.That(width.IsFocused, Is.True);
            Assert.That(w.LayoutXml, Does.Contain("70%"), "Valid input must be published without applying.");
            width.Text = "invalid"; Flush(w);
            Assert.That(w.LayoutXml, Does.Contain("70%"), "Invalid input must preserve the last valid layout.");
            width.Text = "60%"; Flush(w);
            Assert.That(w.LayoutXml, Does.Contain("60%"));
            var expander = w.GetVisualDescendants().OfType<Expander>().Single(e => e.Header?.ToString()?.StartsWith("Condition 1:") == true);
            expander.IsExpanded = true; Flush(w);
            Find<TextBox>(w, "field_when").Text = "value > 80"; Flush(w);
            Assert.That(expander.Header, Is.EqualTo("Condition 1: value > 80"));
            var button = w.GetVisualDescendants().OfType<Button>().First(b => b.Name == "colour_color");
            var swatch = (Border)button.Content!;
            Assert.That(swatch.Bounds.Width, Is.GreaterThanOrEqualTo(28));
            Assert.That(swatch.Bounds.Height, Is.GreaterThanOrEqualTo(28));
            Click(w, button); Flush(w);
            Assert.That(button.Flyout!.IsOpen, Is.True);
            var picker = (ColorView)((Flyout)button.Flyout!).Content!;
            picker.Color = Avalonia.Media.Colors.Lime; Flush(w);
            Assert.That(w.GetVisualDescendants().OfType<TextBox>().First(b => b.Name == "field_color").Text, Is.EqualTo("#00ff00"));
            Assert.That(((Avalonia.Media.SolidColorBrush)swatch.Background!).Color, Is.EqualTo(Avalonia.Media.Colors.Lime));
            button.Flyout.Hide();
        }
        finally { w.HostClosed = true; w.Close(); }
    }
    [AvaloniaTest] public void CardPaddingSelectsButActionButtonsAndDragStartDoNot()
    {
        var w = new EditorWindow(Layout, "{}", true); w.Show(); Flush(w);
        try
        {
            bool Selected(string id) => w.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text?.EndsWith(" · " + id) == true);
            var card = Find<Border>(w, "block_moving");
            // Top padding is outside the caption's hit area.
            var point = card.TranslatePoint(new Point(200, 3), w)!.Value;
            w.MouseDown(point, MouseButton.Left); w.MouseUp(point, MouseButton.Left); Flush(w);
            Assert.That(Selected("moving"), Is.True);
            Click(w, Find<Button>(w, "fold_target"));
            Assert.That(Selected("moving"), Is.True, "Folding a different block must not select it.");
            var target = Find<Border>(w, "block_target");
            var rightPadding = target.TranslatePoint(new Point(target.Bounds.Width - 2, target.Bounds.Height / 2), w)!.Value;
            w.MouseDown(rightPadding, MouseButton.Right); w.MouseUp(rightPadding, MouseButton.Right); Flush(w);
            Assert.That(Selected("moving"), Is.True);
            var grip = Find<Border>(w, "grip_target");
            var start = grip.TranslatePoint(new Point(5, 8), w)!.Value;
            w.MouseDown(start, MouseButton.Left); Flush(w);
            Assert.That(Selected("moving"), Is.True, "Grabbing a block must not change selection.");
            w.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None); w.MouseUp(start, MouseButton.Left);
            w.MouseDown(rightPadding, MouseButton.Left); w.MouseUp(rightPadding, MouseButton.Left); Flush(w);
            Assert.That(Selected("target"), Is.True, "Right padding must also select.");
        }
        finally { w.HostClosed = true; w.Close(); }
    }
    [AvaloniaTest] public void DropFeedbackPaintsBluePixelsBeforeAfterAndInside()
    {
        var w = new EditorWindow(Layout, "{}", true); w.Show(); Flush(w);
        try
        {
            foreach (var y in new[] { .1, .9, .5 })
            {
                var grip = Find<Border>(w, "grip_moving"); var target = Find<Border>(w, "block_target");
                var from = grip.TranslatePoint(new Point(5, 8), w)!.Value;
                var to = target.TranslatePoint(new Point(60, target.Bounds.Height * y), w)!.Value;
                w.MouseDown(from, MouseButton.Left); w.MouseMove(to); Flush(w);
                var marker = Find<Border>(w, "dropIndicator");
                Assert.That(marker.IsVisible, Is.True);
                using var frame = w.CaptureRenderedFrame(); Assert.That(frame, Is.Not.Null);
                var sample = marker.TranslatePoint(new Point(y == .5 ? 1 : marker.Bounds.Width / 2, marker.Bounds.Height / 2), w)!.Value;
                var pixel = new PixelRect((int)(sample.X * w.RenderScaling), (int)(sample.Y * w.RenderScaling), 1, 1);
                var memory = System.Runtime.InteropServices.Marshal.AllocHGlobal(4);
                try
                {
                    frame!.CopyPixels(pixel, memory, 4, 4);
                    var bytes = new byte[4]; System.Runtime.InteropServices.Marshal.Copy(memory, bytes, 0, 4);
                    var bgra = frame.Format == Avalonia.Platform.PixelFormat.Bgra8888;
                    var blue = bytes[bgra ? 0 : 2]; var red = bytes[bgra ? 2 : 0];
                    Assert.That(blue, Is.GreaterThan(red + 80), "Drop marker must actually paint blue pixels; y=" + y);
                }
                finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(memory); }
                // Cancel without moving so every zone uses the same document.
                w.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None); w.MouseUp(to, MouseButton.Left); Flush(w);
            }
        }
        finally { w.HostClosed = true; w.Close(); }
    }
    private const string Layout = "<stack id='root'><layer id='moving'><text id='child'>CPU</text></layer><stack id='target'><text id='other'>42%</text></stack></stack>";
    [AvaloniaTest] public void RgbaPickerRoundTripsAlphaAndEnglishUiPreservesUserText()
    {
        var w = new EditorWindow("<text id='t' color='#12345680'>Drawing</text>", "{}", true); w.Show(); Flush(w);
        try
        {
            Assert.That(w.Title, Is.EqualTo("Custom Button — Layout editor"));
            var button = Find<Button>(w, "colour_color");
            var view = (ColorView)((Flyout)button.Flyout!).Content!;
            Assert.That(view.IsAlphaEnabled, Is.True);
            Assert.That(view.Color, Is.EqualTo(Color.FromArgb(128, 18, 52, 86)));
            view.Color = Color.FromArgb(64, 255, 0, 0); Flush(w);
            Assert.That(Find<TextBox>(w, "field_color").Text, Is.EqualTo("#ff000040"));
            Assert.That(XElement.Parse(w.LayoutXml).Value, Is.EqualTo("Drawing"));
            Find<TextBox>(w, "field_color").Text = "#0f08"; Flush(w);
            Assert.That(view.Color, Is.EqualTo(Color.FromArgb(136, 0, 255, 0)));
        }
        finally { w.HostClosed = true; w.Close(); }
    }
    private static T Find<T>(EditorWindow w, string name) where T : Control => w.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    private static void Flush(EditorWindow w) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    private static void Click(EditorWindow w, Control c)
    {
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value;
        w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left); Flush(w);
    }
    [AvaloniaTest] public void DragMovesTheCapturedContainerAndUndoRestoresItsChildren()
    {
        var w = new EditorWindow(Layout, "{\"value\":42}", true); w.Show(); Flush(w);
        try
        {
            var source = Find<Border>(w, "grip_moving"); var target = Find<Border>(w, "block_target");
            var from = source.TranslatePoint(new Point(5, 8), w)!.Value;
            var to = target.TranslatePoint(new Point(60, target.Bounds.Height - 2), w)!.Value;
            var offset = Find<ScrollViewer>(w, "blocksScroll").Offset;
            w.MouseDown(from, MouseButton.Left); Flush(w);
            Assert.That(Find<ScrollViewer>(w, "blocksScroll").Offset, Is.EqualTo(offset), "Pressing grip must not auto-scroll/select/rebuild.");
            w.MouseMove(to); w.MouseUp(to, MouseButton.Left); Flush(w);
            var doc = XElement.Parse(w.LayoutXml);
            Assert.That(doc.Elements().Select(n => (string?)n.Attribute("id")), Is.EqualTo(new[] { "target", "moving" }));
            Assert.That(doc.Elements().Last().Element("text")!.Value, Is.EqualTo("CPU"));
            Click(w, Find<Button>(w, "undo")); Assert.That(w.LayoutXml, Is.EqualTo(Layout));
        }
        finally { w.HostClosed = true; w.Close(); }
    }
    [AvaloniaTest] public void ColourEditorAndFoldRenderWithoutWindowsControls()
    {
        var w = new EditorWindow(Layout, "{}", true); w.Show(); Flush(w);
        try
        {
            Assert.That(w.GetVisualDescendants().OfType<Button>().Any(b => b.Name == "colour_background"), Is.True);
            Click(w, Find<Button>(w, "fold_moving"));
            Assert.That(w.GetVisualDescendants().OfType<Control>().Any(c => c.Name == "block_child"), Is.False);
            Assert.That(w.LayoutXml, Is.EqualTo(Layout));
            Click(w, Find<Button>(w, "fold_moving"));
            Assert.That(Find<Border>(w, "block_child"), Is.Not.Null);
            var output = Environment.GetEnvironmentVariable("EDITOR_SNAPSHOT");
            if (output != null) { using var image = w.CaptureRenderedFrame(); Assert.That(image, Is.Not.Null); image!.Save(output); }
        }
        finally { w.HostClosed = true; w.Close(); }
    }
    [AvaloniaTest] public void ScrollDuringDragCannotChangeTheCapturedIdentity()
    {
        var layout = "<stack id='root'>" + string.Concat(Enumerable.Range(0, 8).Select(i => $"<text id='t{i}'>Text</text>"))
            + "<layer id='moving'><text id='child'>Keep</text></layer><text id='target'>Target</text>"
            + string.Concat(Enumerable.Range(8, 12).Select(i => $"<text id='t{i}'>Text</text>")) + "</stack>";
        var w = new EditorWindow(layout, "{}", true); w.Show(); Flush(w);
        try
        {
            var s = Find<ScrollViewer>(w, "blocksScroll"); s.Offset = new Vector(0, 250); Flush(w);
            var grip = Find<Border>(w, "grip_moving"); var from = grip.TranslatePoint(new Point(6, 8), w)!.Value;
            w.MouseDown(from, MouseButton.Left); Flush(w);
            Assert.That(s.Offset.Y, Is.EqualTo(250));
            s.Offset = new Vector(0, 300); Flush(w);
            var target = Find<Border>(w, "block_target"); var to = target.TranslatePoint(new Point(60, target.Bounds.Height - 2), w)!.Value;
            w.MouseMove(to); w.MouseUp(to, MouseButton.Left); Flush(w);
            var doc = new LayoutDocument(w.LayoutXml);
            Assert.That(doc.Find("moving").ElementsBeforeSelf().Last().Attribute("id")!.Value, Is.EqualTo("target"));
            Assert.That(doc.Find("child").Parent, Is.SameAs(doc.Find("moving")));
        }
        finally { w.HostClosed = true; w.Close(); }
    }
    [AvaloniaTest] public void AttributeEditingAndXmlKeepConditionsAndUndo()
    {
        var layout = "<text id='title' color='#ffffff'>CPU<style when='value > 80' color='#ff0000'/></text>";
        var w = new EditorWindow(layout, "{\"value\":42}", true); w.Show(); Flush(w);
        try
        {
            Find<TextBox>(w, "field_text").Text = "{{value}}%";
            Flush(w);
            var node = XElement.Parse(w.LayoutXml);
            Assert.That(node.Value, Is.EqualTo("{{value}}%"));
            Assert.That(node.Element("style")!.Attribute("color")!.Value, Is.EqualTo("#ff0000"));
            Click(w, Find<Button>(w, "undo")); Assert.That(w.LayoutXml, Is.EqualTo(layout));
        }
        finally { w.HostClosed = true; w.Close(); }
    }
}



