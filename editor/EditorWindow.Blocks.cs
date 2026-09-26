using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Ziopuzzle.CustomButton;

public sealed partial class EditorWindow
{
    private void AddRows(XElement node, int depth)
    {
        var id = Id(node); var type = node.Name.LocalName; var container = LayoutDocument.IsContainer(node);
        var card = new Border { Name = "block_" + id, Margin = new Thickness(depth * 20, 0, 0, 0), Background = B(id == selected ? "#26323b" : "#202020"), BorderBrush = B(Colour(type)), BorderThickness = new Thickness(3, 0, 0, 0), CornerRadius = new CornerRadius(5), Padding = new Thickness(8), MinHeight = 42 };
        var line = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 8 };
        var handle = new Border { Name = "grip_" + id, Background = Brushes.Transparent, Padding = new Thickness(4, 0), Child = new TextBlock { Text = "⠿", FontSize = 18, Foreground = B("#aaaaaa") }, Cursor = new Cursor(StandardCursorType.SizeAll), Focusable = false };
        line.Children.Add(handle);
        var caption = new TextBlock { Name = "caption_" + id, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        UpdateCaption(caption, node);
        card.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(card).Properties.IsLeftButtonPressed) return;
            // Let grips and action buttons handle their own gestures without selecting/rebuilding.
            for (var origin = e.Source as Visual; origin != null && origin != card; origin = origin.GetVisualParent())
                if (origin == handle || origin is Button) return;
            Guard(() => Select(id)); e.Handled = true;
        };
        Grid.SetColumn(caption, 1); line.Children.Add(caption);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        if (container) actions.Children.Add(Button(collapsed.Contains(id) ? "›" : "⌄", () =>
        {
            if (!ResolvePending()) return;
            if (!collapsed.Add(id)) collapsed.Remove(id);
            else if (new LayoutDocument(history.Xml).Find(selected).Ancestors().Any(a => Id(a) == id)) selected = id;
            Refresh();
        }, "fold_" + id));
        var more = Button("⋯", () => { }); more.Click += (_, _) =>
        {
            var menu = new ContextMenu();
            void Command(string label, Func<LayoutDocument, string> change, bool enabled = true)
            {
                var entry = new MenuItem { Header = T(label), IsEnabled = enabled }; entry.Click += (_, _) => Guard(() => Edit(change)); menu.Items.Add(entry);
            }
            Command("Duplicate", d => d.Duplicate(id), node.Parent != null); Command("Wrap in stack", d => d.Wrap(id)); Command("Wrap in layer", d => d.Wrap(id, "layer")); Command("Delete", d => d.Delete(id), node.Parent != null);
            more.ContextMenu = menu; menu.Open(more);
        };
        actions.Children.Add(more); Grid.SetColumn(actions, 2); line.Children.Add(actions); card.Child = line;
        rows.Children.Add(card); targets.Add(new(id, card, container, node.Parent == null, false));
        handle.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed || propertyDirty || xmlDirty) { if (propertyDirty || xmlDirty) Say("Correct or reset the pending input before moving."); return; }
            // Identity is fixed before any focus/scroll change. No selecting or rebuilding during drag.
            dragId = id; dragStart = e.GetPosition(canvas); dragging = false; dragPointer = e.Pointer; e.Pointer.Capture(handle); e.Handled = true;
        };
        handle.PointerMoved += (_, e) =>
        {
            if (dragId != id || e.Pointer.Captured != handle) return;
            var point = e.GetPosition(canvas);
            if (!dragging && Math.Abs(point.X - dragStart.X) < 5 && Math.Abs(point.Y - dragStart.Y) < 5) return;
            dragging = true; UpdateDrop(point); e.Handled = true;
        };
        handle.PointerReleased += (_, e) =>
        {
            if (dragId != id) return;
            var source = dragId; var target = dropRow; var zone = placement; var moved = dragging;
            EndDrag(); e.Pointer.Capture(null); e.Handled = true;
            if (!moved) Guard(() => Select(source));
            else if (target != null && zone != null) Guard(() => Edit(d => { d.Place(source, target.Id, zone); return source; }));
        };
        handle.PointerCaptureLost += (_, _) => EndDrag();
        if (!container || collapsed.Contains(id)) return;
        foreach (var child in node.Elements().Where(n => n.Name != "style")) AddRows(child, depth + 1);
        var add = AddButton(() => id); add.HorizontalAlignment = HorizontalAlignment.Left;
        var footer = new Border { Margin = new Thickness((depth + 1) * 20, 0, 0, 0), Child = add, Background = Brushes.Transparent };
        rows.Children.Add(footer); targets.Add(new(id, footer, true, node.Parent == null, true));
    }
    private void UpdateCaption(TextBlock caption, XElement node)
    {
        var type = node.Name.LocalName;
        var summary = type == "text" ? string.Concat(node.Nodes().OfType<XText>().Select(n => n.Value)) : string.Join("  ", node.Attributes().Where(a => a.Name != "id").Select(a => a.Name + ": " + a.Value));
        if (node.Elements("style").Any()) summary += " · " + string.Join(" / ", node.Elements("style").Select(s => T("Condition: ") + (string?)s.Attribute("when")));
        caption.Inlines = new InlineCollection();
        caption.Inlines.Add(new Run(T(Types[type])) { Foreground = B("#eeeeee"), FontWeight = FontWeight.SemiBold });
        caption.Inlines.Add(new Run("   " + summary.Replace('\n', ' ').Replace('\r', ' ')) { Foreground = B("#aaaaaa") });
        ToolTip.SetTip(caption, Id(node) + " : " + summary);
    }
    private void EndDrag()
    {
        dragId = null; dragging = false; dropRow = null; placement = null; indicator.IsVisible = false;
        var pointer = dragPointer; dragPointer = null; pointer?.Capture(null);
    }
    private void UpdateDrop(Point point)
    {
        dropRow = null; placement = null; indicator.IsVisible = false;
        if (point.Y < 22) scroll.Offset = scroll.Offset.WithY(Math.Max(0, scroll.Offset.Y - 8));
        else if (point.Y > canvas.Bounds.Height - 22) scroll.Offset = scroll.Offset.WithY(scroll.Offset.Y + 8);
        foreach (var row in targets)
        {
            var origin = row.Card.TranslatePoint(default, canvas); if (origin == null) continue;
            var rect = new Rect(origin.Value, row.Card.Bounds.Size);
            if (point.Y < rect.Top - 3 || point.Y > rect.Bottom + 3 || point.X < rect.Left || point.X > rect.Right) continue;
            var y = (point.Y - rect.Top) / Math.Max(1, rect.Height);
            var zone = row.Footer ? (!row.Root && y > .75 ? "after" : "inside") : LayoutDropPlacement.Header(row.Container, row.Root, y);
            try { var doc = new LayoutDocument(history.Xml); doc.Place(dragId!, row.Id, zone); _ = doc.Serialize(); }
            catch (FormatException e) { Say(e.Message); return; }
            dropRow = row; placement = zone;
            var inside = zone == "inside";
            indicator.Margin = new Thickness(rect.Left, zone == "after" ? rect.Bottom - 3 : rect.Top, 0, 0);
            indicator.Width = rect.Width; indicator.Height = inside ? rect.Height : 3;
            // A two-pixel-high Border with two-pixel edges can collapse its border geometry.
            // Insertions are solid strips; nesting remains an unfilled outline.
            indicator.BorderThickness = inside ? new Thickness(2) : new Thickness(0);
            indicator.Background = inside ? Brushes.Transparent : B("#2196f3"); indicator.IsVisible = true;
            Say(row.Id + (zone == "inside" ? T(": place inside") : zone == "before" ? T(": place before") : T(": place after"))); return;
        }
    }
}

