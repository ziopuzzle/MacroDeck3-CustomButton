using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace Ziopuzzle.CustomButton;

public sealed partial class EditorWindow
{
    private sealed record PropertyField(TextBox Box, TextBlock Label, string Caption);
    private readonly Dictionary<(int? Condition, string Name), PropertyField> propertyFields = new();
    private PropertyField? markedField;

    private void UpdateFieldWarning(EditorValidation.Issue? issue)
    {
        var field = issue?.Id == selected && issue.Field != null ? propertyFields.GetValueOrDefault((issue.Condition, issue.Field)) : null;
        if (markedField != field && markedField != null)
        {
            markedField.Label.Text = markedField.Caption;
            markedField.Label.Foreground = B("#bbbbbb");
            ToolTip.SetTip(markedField.Label, null);
        }
        markedField = field;
        if (field == null) return;
        field.Label.Text = "⚠ " + field.Caption;
        field.Label.Foreground = B("#ffc266");
        ToolTip.SetTip(field.Label, issue!.Message);
    }

    private void FocusIssueField()
    {
        if (markedField is not { } field) return;
        foreach (var section in field.Box.GetLogicalAncestors().OfType<Expander>().Reverse()) section.IsExpanded = true;
        UpdateLayout();
        field.Box.Focus();
        field.Box.BringIntoView();
    }

    private TextBox Field(StackPanel panel, string label, string value, string attribute, string component, bool inherited = false, int? condition = null)
    {
        var caption = new TextBlock { Name = "label_" + (condition.HasValue ? condition + "_" : "") + attribute, Text = T(label), Foreground = B("#bbbbbb") };
        panel.Children.Add(caption);
        var row = new DockPanel { LastChildFill = true };
        var box = new TextBox { Name = "field_" + attribute, Text = value, Watermark = inherited ? null : PropertyDefaults.Get(component, attribute), MinWidth = 40, Padding = new Thickness(7, 5) };
        propertyFields[(condition, attribute)] = new(box, caption, T(label));
        if (component == "svg" && attribute == "text")
        {
            box.AcceptsReturn = true; box.TextWrapping = TextWrapping.Wrap;
            box.MinHeight = 180; box.MaxHeight = 420;
        }
        ToolTip.SetTip(box, PropertyDescriptions.Help(component, attribute) ?? T(PropertyDescriptions.DefaultHelp));
        if (attribute == "source") AddImageBrowser(row, box);
        if (attribute.EndsWith("color", StringComparison.OrdinalIgnoreCase) || attribute is "background" or "averageColorFallback")
            AddColorPicker(row, box, attribute, value);
        var options = LayoutOptions.Choices(component, attribute);
        if (options.Length > 0) AddValueMenu(row, box, attribute, "▾", "Suggested values", new[] { ("Use default", "") }.Concat(options.Select(o => (o, o))));
        if (ExpressionInsertion.KeyOnly(attribute) || attribute == "items")
            AddValueMenu(row, box, attribute, "▾", "Select a display data name (replaces the field; no value binding)",
                new[] { ("Clear", "") }.Concat(DisplayDataNames(includeRepeated: attribute == "items").Select(k => (k, k))));
        if (!ExpressionInsertion.KeyOnly(attribute) && ExpressionInsertion.Available(attribute)) AddExpressionPicker(row, box, attribute);
        var groupKey = Guid.NewGuid().ToString();
        box.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty && !rebuilding) { propertyGroup = groupKey; propertyDirty = true; updateDraftDescription?.Invoke(); } }; row.Children.Add(box); panel.Children.Add(row); return box;
    }

    private void AddImageBrowser(DockPanel row, TextBox box)
    {
        var browse = Button("Browse…", () => { });
        browse.Click += async (_, _) =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Choose an image", AllowMultiple = false,
                FileTypeFilter = new[] { new Avalonia.Platform.Storage.FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.gif" } } }
            });
            if (files.Count > 0 && files[0].Path.IsFile) box.Text = files[0].Path.LocalPath;
        };
        DockPanel.SetDock(browse, Dock.Right); row.Children.Add(browse);
    }

    private void AddColorPicker(DockPanel row, TextBox box, string attribute, string value)
    {
        var colour = new ColorView { IsAlphaEnabled = true };
        var swatch = new Border { Width = 28, Height = 28, CornerRadius = new CornerRadius(3) };
        var colourButton = new Button { Name = "colour_" + attribute, Content = swatch, Padding = new Thickness(3), Margin = new Thickness(0, 0, 4, 0) };
        var flyout = new Flyout { Content = colour };
        colourButton.Flyout = flyout;
        ToolTip.SetTip(colourButton, T("Click to choose a colour"));
        static bool ParseColor(string? value, out Color result)
        {
            try { var c = DisplayColor.Parse(value ?? ""); result = Color.Parse("#" + c.Alpha.ToString("x2") + c.Rgb[1..]); return true; }
            catch (FormatException) { result = default; return false; }
        }
        if (ParseColor(value, out var initial)) colour.Color = initial;
        swatch.Background = new SolidColorBrush(colour.Color);
        var synchronizing = false;
        colour.ColorChanged += (_, _) => { swatch.Background = new SolidColorBrush(colour.Color); if (!synchronizing) box.Text = new DisplayColor($"#{colour.Color.R:x2}{colour.Color.G:x2}{colour.Color.B:x2}", colour.Color.A).Hex; };
        box.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty && ParseColor(box.Text, out var c) && colour.Color != c) { synchronizing = true; colour.Color = c; synchronizing = false; } };
        DockPanel.SetDock(colourButton, Dock.Left); row.Children.Add(colourButton);
    }

    private void AddValueMenu(DockPanel row, TextBox box, string attribute, string title, string tooltip, IEnumerable<(string Label, string Value)> entries)
    {
        var button = Button(title, () => { }); ToolTip.SetTip(button, T(tooltip)); button.Margin = new Thickness(4, 0, 0, 0);
        if (ExpressionInsertion.KeyOnly(attribute)) button.Name = "select_" + attribute;
        button.Click += (_, _) =>
        {
            var menu = new ContextMenu();
            foreach (var entry in entries)
            {
                var item = new MenuItem { Header = T(entry.Label), ToggleType = MenuItemToggleType.Radio, IsChecked = box.Text == entry.Value };
                item.Click += (_, _) => { box.Text = entry.Value; box.Focus(); }; menu.Items.Add(item);
            }
            button.ContextMenu = menu; menu.Open(button);
        };
        DockPanel.SetDock(button, Dock.Right); row.Children.Add(button);
    }

    private void AddExpressionPicker(DockPanel row, TextBox box, string attribute)
    {
        var insert = Button("{ }", () => { }, "insert_" + attribute);
        insert.Focusable = false; // Keep the text selection until the picker has captured it.
        ToolTip.SetTip(insert, "Insert display data, a function or an operator.");
        insert.Margin = new Thickness(4, 0, 0, 0);
        insert.Click += async (_, _) => { try { await InsertExpression(box, attribute); } catch (FormatException e) { Say("⚠ " + e.Message); } };
        DockPanel.SetDock(insert, Dock.Right); row.Children.Add(insert);
    }

    private IEnumerable<string> DisplayDataNames(bool includeRepeated = false)
    {
        var names = keys.Concat(new LayoutRenderer(history.Xml).ImageColorDefaults.Keys);
        if (includeRepeated) names = names.Concat(LayoutRenderer.RepeatPreviewKeys(history.Xml, selected, initialValues));
        return names.Distinct().OrderBy(k => k);
    }
}
