using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Ziopuzzle.CustomButton;

public sealed partial class EditorWindow
{
    private async Task InsertExpression(TextBox box, string attribute)
    {
        var original = box.Text ?? "";
        var start = box.SelectionStart; var end = box.SelectionEnd;
        var choices = keys.Concat(new LayoutRenderer(history.Xml).ImageColorDefaults.Keys).Distinct().OrderBy(k => k)
            .Select(k => new ExpressionChoice("Display data", k, "Display data key. This is Channel data, not a direct Macro Deck variable lookup.", k))
            .Concat(ExpressionInsertion.KeyOnly(attribute) ? [] : ExpressionInsertion.Functions.Where(f =>
                ExpressionInsertion.IsCondition(attribute) ? f.Condition : f.Function != "type")).ToArray();
        var dialog = new Window { Title = "Edit value — display data and functions", Width = 680, Height = 700, MinWidth = 480, MinHeight = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = B("#202020"), Foreground = B("#eeeeee") };
        var panel = new Grid { Margin = new Thickness(16), RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto,Auto,Auto,Auto"), RowSpacing = 12 };
        var search = new TextBox { Watermark = "Search names, categories or descriptions" }; panel.Children.Add(search);
        var list = new ListBox { ItemsSource = choices }; Grid.SetRow(list, 1); panel.Children.Add(list);
        var description = new TextBlock { TextWrapping = TextWrapping.Wrap, MinHeight = 58 }; Grid.SetRow(description, 2); panel.Children.Add(description);
        var edit = new TextBox { Name = "expressionValue", Text = original, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("monospace"), MinHeight = 110, MaxHeight = 200,
            IsInactiveSelectionHighlightEnabled = true, SelectionBrush = B("#23679c") };
        Grid.SetRow(edit, 3); panel.Children.Add(edit);
        var position = new TextBlock { Name = "expressionPosition", TextWrapping = TextWrapping.Wrap };
        Grid.SetRow(position, 4); panel.Children.Add(position);
        var feedback = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = B("#e9b85e") };
        Grid.SetRow(feedback, 5); panel.Children.Add(feedback);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        var insert = new Button { Content = "Insert", IsEnabled = false, Focusable = false };
        buttons.Children.Add(insert); Grid.SetRow(buttons, 6); panel.Children.Add(buttons);
        void Position()
        {
            var text = edit.Text ?? "";
            start = Math.Clamp(start, 0, text.Length); end = Math.Clamp(end, 0, text.Length);
            var caret = Math.Min(start, end);
            var line = text[..caret].Count(c => c == '\n') + 1;
            var previousLine = caret == 0 ? -1 : text.LastIndexOf('\n', caret - 1);
            position.Text = $"Insertion: line {line}, column {caret - previousLine}" +
                (start == end ? "." : $" · {Math.Abs(end - start)} characters selected.") +
                " Changes update the main GUI live. Close with × to keep edits. Save in Macro Deck to finish.";
        }
        void Update()
        {
            insert.IsEnabled = false;
            if (list.SelectedItem is not ExpressionChoice choice) { description.Text = "No matching items. You can still edit the value directly."; return; }
            description.Text = choice.Description + "\nExample: " + choice.Example;
            insert.IsEnabled = true;
        }
        search.TextChanged += (_, _) =>
        {
            var query = search.Text ?? "";
            list.ItemsSource = choices.Where(c => (c + " " + c.Description).Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
            list.SelectedIndex = 0; Update();
        };
        list.SelectionChanged += (_, _) => Update();
        edit.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.SelectionStartProperty || e.Property == TextBox.SelectionEndProperty)
            {
                if (edit.IsFocused) { start = edit.SelectionStart; end = edit.SelectionEnd; Position(); }
            }
            if (e.Property == TextBox.TextProperty)
            {
                // Use the normal property pipeline, including validation, undo grouping and
                // the existing throttled host preview updates. Never steal focus while typing.
                box.Text = edit.Text;
                feedback.Text = propertyDirty ? status.Text : previewWarning ?? "";
                Position();
            }
        };
        insert.Click += (_, _) =>
        {
            if (list.SelectedItem is not ExpressionChoice choice) return;
            try
            {
                var result = ExpressionInsertion.Insert(edit.Text ?? "", start, end, attribute, choice);
                edit.Text = result.Text; edit.Focus();
                edit.SelectionStart = result.Start; edit.SelectionEnd = result.End;
                start = result.Start; end = result.End; Position();
            }
            catch (FormatException e) { feedback.Text = e.Message; }
        };
        dialog.Opened += (_, _) =>
        {
            var initialStart = start; var initialEnd = end;
            edit.Focus(); edit.SelectionStart = initialStart; edit.SelectionEnd = initialEnd;
            start = initialStart; end = initialEnd; Position();
        };
        dialog.Content = panel; list.SelectedIndex = 0; Update(); Position();
        await dialog.ShowDialog(this);
        box.Focus(); box.SelectionStart = start; box.SelectionEnd = end;
    }
}
