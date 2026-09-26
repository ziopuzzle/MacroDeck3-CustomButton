using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Ziopuzzle.CustomButton;

public sealed partial class EditorWindow
{
    private Action? updateDraftDescription;
    private void MakeProperties()
    {
        updateDraftDescription = null;
        properties.Children.Clear(); propertyDirty = false;
        var node = new LayoutDocument(history.Xml).Find(selected);
        var originalXml = history.Xml;
        var coalesce = false;
        var heading = new TextBlock { Text = T(Types[node.Name.LocalName]) + " · " + selected, FontSize = 17, FontWeight = FontWeight.SemiBold };
        properties.Children.Add(heading);
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        tools.Children.Add(Button("Reset input", () => { history.Set(originalXml, coalesce); propertyDirty = false; Publish(); Refresh(); })); properties.Children.Add(tools);
        properties.Children.Add(new TextBlock { Text = T("Valid settings are applied automatically."), Foreground = B("#bbbbbb") });
        var attributes = new Dictionary<string, TextBox>(); TextBox? text = null;
        if (node.Name == "text") text = Field(properties, "Display text", string.Concat(node.Nodes().OfType<XText>().Select(n => n.Value)), "text", node.Name.LocalName);
        var allowed = LayoutRenderer.Allowed(node.Name.LocalName).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var common = new StackPanel { Spacing = 6 };
        var transitions = new StackPanel { Spacing = 6 };
        var geometryNames = new[] { new[] { "x1", "y1", "x2", "y2" }, new[] { "cx", "cy", "length", "angle" }, new[] { "x", "y", "length", "direction" } };
        var geometry = new StackPanel { Spacing = 6 };
        ComboBox? lineMode = null;
        if (node.Name == "line")
        {
            lineMode = new ComboBox { Name = "lineMode", ItemsSource = new[] { "Two endpoints", "Start + length + angle", "Horizontal / vertical" },
                SelectedIndex = node.Attribute("angle") != null ? 1 : node.Attributes().Any(a => geometryNames[0].Contains(a.Name.LocalName)) ? 0 : 2,
                HorizontalAlignment = HorizontalAlignment.Stretch };
            properties.Children.Add(new TextBlock { Text = "Line geometry" });
            properties.Children.Add(lineMode); properties.Children.Add(geometry);
        }
        var geometryFields = new Dictionary<string, StackPanel>();
        foreach (var attribute in allowed)
        {
            var panel = attribute is "transitionMs" or "transitionProperties" or "easing" or "colorSpace" ? transitions : attribute is "fill" or "mainSize" or "visible" or "visibleWhen" ? common : properties;
            if (lineMode != null && geometryNames.Any(names => names.Contains(attribute)))
            {
                panel = new StackPanel { Spacing = 6 }; geometry.Children.Add(panel); geometryFields[attribute] = panel;
            }
            attributes[attribute] = Field(panel, Label(attribute), (string?)node.Attribute(attribute) ?? "", attribute, node.Name.LocalName);
        }
        void ShowGeometry()
        {
            if (lineMode == null) return;
            foreach (var pair in geometryFields) pair.Value.IsVisible = geometryNames[lineMode.SelectedIndex].Contains(pair.Key);
        }
        ShowGeometry();
        properties.Children.Add(new Expander { Header = T("Size and visibility"), Content = common, HorizontalAlignment = HorizontalAlignment.Stretch });
        properties.Children.Add(new Expander { Header = T("Transitions"), Content = transitions, HorizontalAlignment = HorizontalAlignment.Stretch });
        var rules = new List<Dictionary<string, TextBox>>();
        var ruleHeaders = new List<Expander>();
        foreach (var style in node.Elements("style"))
        {
            var fields = new Dictionary<string, TextBox>(); var panel = new StackPanel { Spacing = 6 };
            fields["when"] = Field(panel, "Condition expression", (string?)style.Attribute("when") ?? "", "when", node.Name.LocalName);
            foreach (var attribute in allowed.Where(a => a != "id")) fields[attribute] = Field(panel, Label(attribute), (string?)style.Attribute(attribute) ?? "", attribute, node.Name.LocalName);
            var index = rules.Count; panel.Children.Add(Button("Delete this condition", () => Edit(d => { d.DeleteStyle(selected, index); return selected; })));
            var header = new Expander { Header = $"{T("Condition")} {index + 1}: {(string?)style.Attribute("when")}", Content = panel, HorizontalAlignment = HorizontalAlignment.Stretch };
            properties.Children.Add(header); ruleHeaders.Add(header); rules.Add(fields);
        }
        updateDraftDescription = () =>
        {
            var draft = new XElement(node);
            if (text != null) { draft.Nodes().OfType<XText>().Remove(); draft.AddFirst(new XText(text.Text ?? "")); }
            foreach (var pair in attributes) draft.SetAttributeValue(pair.Key, string.IsNullOrWhiteSpace(pair.Value.Text) ? null : pair.Value.Text);
            if (lineMode != null)
                foreach (var name in geometryFields.Keys.Where(name => !geometryNames[lineMode.SelectedIndex].Contains(name))) draft.SetAttributeValue(name, null);
            for (var i = 0; i < rules.Count; i++)
            {
                foreach (var pair in rules[i]) draft.Elements("style").ElementAt(i).SetAttributeValue(pair.Key, string.IsNullOrWhiteSpace(pair.Value.Text) ? null : pair.Value.Text);
                ruleHeaders[i].Header = $"{T("Condition")} {i + 1}: {rules[i]["when"].Text}";
            }
            var caption = rows.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Name == "caption_" + selected);
            if (caption != null) UpdateCaption(caption, draft);
            if (!propertyDirty) return;
            try
            {
                var document = new LayoutDocument(history.Xml);
                var current = document.Find(selected);
                string next;
                if (current.Parent == null) next = new LayoutDocument(draft.ToString()).Serialize();
                else { current.ReplaceWith(draft); next = document.Serialize(); }
                _ = new LayoutRenderer(next).Render(initialValues);
                if (next != history.Xml) { history.Set(next, coalesce); coalesce = true; }
                var renamed = selected != Id(draft);
                if (renamed)
                {
                    if (collapsed.Remove(selected)) collapsed.Add(Id(draft));
                    selected = Id(draft);
                    heading.Text = T(Types[node.Name.LocalName]) + " · " + selected;
                    var offset = scroll.Offset;
                    targets.Clear(); rows.Children.Clear(); AddRows(new LayoutDocument(next).Root, 0);
                    scroll.Offset = offset;
                }
                propertyDirty = false;
                rebuilding = true; xml.Text = history.Xml; rebuilding = false; xmlDirty = false;
                Publish();
            }
            catch (Exception e) when (e is FormatException or System.Xml.XmlException or InvalidOperationException)
            { Say("Input incomplete: " + e.Message + " The last valid display is kept."); }
        };
        applyProperties = () =>
        {
            updateDraftDescription();
            if (propertyDirty) throw new FormatException("Please correct the input.");
        };
        properties.Children.Add(Button("＋ Conditional style", () => Edit(d => { d.AddStyle(selected); return selected; })));
        if (lineMode != null) lineMode.SelectionChanged += (_, _) =>
        {
            if (lineMode.SelectedIndex < 0) return;
            rebuilding = true;
            var defaults = lineMode.SelectedIndex == 0
                ? new Dictionary<string, string> { ["x1"] = "10%", ["y1"] = "50%", ["x2"] = "90%", ["y2"] = "50%" }
                : lineMode.SelectedIndex == 1 ? new Dictionary<string, string> { ["angle"] = "0" } : new Dictionary<string, string>();
            foreach (var pair in defaults) if (string.IsNullOrWhiteSpace(attributes[pair.Key].Text)) attributes[pair.Key].Text = pair.Value;
            rebuilding = false; ShowGeometry(); propertyDirty = true; updateDraftDescription();
        };
        updateDraftDescription();
    }
    private TextBox Field(StackPanel panel, string label, string value, string attribute, string component)
    {
        panel.Children.Add(new TextBlock { Text = T(label), Foreground = B("#bbbbbb") });
        var row = new DockPanel { LastChildFill = true };
        var box = new TextBox { Name = "field_" + attribute, Text = value, MinWidth = 40, Padding = new Thickness(7, 5) };
        ToolTip.SetTip(box, T("Leave blank for the default. Use {{dataName}}, {{= used / total * 100:F1}}%, or {{= floor(position / 1000):duration}} for milliseconds. Math supports floor, ceil, round, abs, min, max, clamp and more."));
        if (attribute == "id") ToolTip.SetTip(box, "Unique element ID. Valid edits apply automatically. Update matching action-event filters separately.");
        if (attribute == "transitionMs") ToolTip.SetTip(box, "0 disables transitions. Range: 0–10000 ms. Only later changes animate; the initial value appears immediately.");
        if (attribute == "angle") ToolTip.SetTip(box, "Degrees: 0 points right, 90 down. Use cx/cy/length; clear x1/y1/x2/y2 and x/y/direction. Animate angle to rotate a needle without shortening it. Use a square box for a circular sweep.");
        if (attribute is "offsetX" or "offsetY") ToolTip.SetTip(box, "Use a fraction from -1 to 1: 0.2 moves by 20% of the image box. Positive X moves right; positive Y moves down. The offset is applied after zoom. Supports data bindings and transitions.");
        if (attribute == "zoom") ToolTip.SetTip(box, "Multiplier after contain/cover fitting. 1 is normal; 1.5 is 150%. Range: 0.1–4. Supports data bindings and transitions.");
        if (attribute == "fontFace") ToolTip.SetTip(box, "Macro Deck font catalogue ID, not a Windows font name or file path. Use {{fontId}} for display data. Leave blank for the default font; unavailable IDs fall back to the client's default. Copy the ID from a standard button's saved font settings.");
        if (attribute == "source")
        {
            ToolTip.SetTip(box, "An absolute file path, HTTP/HTTPS URL, Macro Deck /api/... URL, or icon-pack:UUID. Copy the UUID from icon.reference in a standard button's saved JSON. Use {{imageUrl}} for changing artwork. PNG, JPEG, WebP and GIF, up to 2 MiB. Blank hides the image.");
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
        if (attribute == "transitionProperties") ToolTip.SetTip(box, "Separate names with spaces, e.g. opacity color. Default: opacity. Only properties supported by this component are accepted.");
        if (attribute.EndsWith("color", StringComparison.OrdinalIgnoreCase) || attribute == "background")
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
        void Menu(string title, string tooltip, IEnumerable<(string Label, string Value)> entries, bool insert)
        {
            var button = Button(title, () => { }); ToolTip.SetTip(button, T(tooltip)); button.Margin = new Thickness(4, 0, 0, 0);
            button.Click += (_, _) =>
            {
                var menu = new ContextMenu();
                foreach (var entry in entries)
                {
                    var item = new MenuItem { Header = insert ? entry.Label : T(entry.Label), ToggleType = insert ? MenuItemToggleType.None : MenuItemToggleType.Radio, IsChecked = !insert && box.Text == entry.Value };
                    item.Click += (_, _) => { if (insert) box.SelectedText = entry.Value; else box.Text = entry.Value; box.Focus(); }; menu.Items.Add(item);
                }
                button.ContextMenu = menu; menu.Open(button);
            };
            DockPanel.SetDock(button, Dock.Right); row.Children.Add(button);
        }
        var options = LayoutOptions.Choices(component, attribute);
        if (options.Length > 0) Menu("▾", "Suggested values", new[] { ("Use default", "") }.Concat(options.Select(o => (o, o))), false);
        if (keys.Length > 0 && attribute != "id") Menu("{ }", "Insert display data", keys.Select(k => { var v = attribute is "key" or "when" or "visibleWhen" ? k : "{{" + k + "}}"; return (v, v); }), true);
        box.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty && !rebuilding) { propertyDirty = true; updateDraftDescription?.Invoke(); } }; row.Children.Add(box); panel.Children.Add(row); return box;
    }
    private static string Label(string attribute) => attribute switch
    {
        "id" => "Element ID",
        "angle" => "Line angle (degrees)",
        "fit" => "Image fit (contain / cover)",
        "zoom" => "Image zoom (0.1–4, 1 = normal)", "offsetX" => "Image horizontal offset (−1–1)", "offsetY" => "Image vertical offset (−1–1)",
        "source" => "Image source (file / URL / Icon Pack)", "transition" => "Image change transition", "brightness" => "Brightness (0–2)", "saturation" => "Saturation (0–2)",
        "coordinates" => "Coordinates (widget basis / local box)",
        "fontFace" => "Font (Macro Deck catalogue ID)", "weight" => "Font weight (regular / medium / semibold / bold)",
        "x1" => "Start X (0–100%)", "y1" => "Start Y (0–100%)", "x2" => "End X (0–100%)", "y2" => "End Y (0–100%)",
        "gradient" => "Fill gradient", "gradientAngle" => "Gradient angle (0 up, 90 right)", "gradientX" => "Radial center X (0–100%)", "gradientY" => "Radial center Y (0–100%)",
        "transitionMs" => "Transition duration (ms)", "transitionProperties" => "Transition properties (space separated)", "easing" => "Transition easing", "colorSpace" => "Color interpolation space",
        "interactive" => "Enable interaction", "step" => "Value step", "key" => "Data key (chart / slider)",
        "opacity" => "Opacity (0–1)", "strokeColor" => "Stroke color", "strokeWidth" => "Stroke width", "cornerRadius" => "Corner radius", "data" => "Path (absolute coordinates)",
        "cx" => "Center X", "cy" => "Center Y", "radius" => "Radius", "startAngle" => "Start angle (degrees)", "sweepAngle" => "Sweep angle (degrees)", "points" => "Point count / polygon coordinates (x,y; x,y; …)",
        "x" => "Position from left", "y" => "Position from top", "width" => "Width", "height" => "Height", "length" => "Line length", "corner" => "Corners (square / rounded)",
        "direction" => "Direction", "color" => "Colour", "background" => "Background colour", "size" => "Size (e.g. 18%)", "mainSize" => "Main-axis size", "fill" => "Fill remaining space", "visible" => "Visible", "visibleWhen" => "Visibility condition", "padding" => "Padding", "gap" => "Gap between components", "align" => "Cross-axis alignment", "justify" => "Main-axis alignment", "borderStyle" => "Border style", "borderColor" => "Border colour", "value" => "Display value", "min" => "Minimum", "max" => "Maximum", "thickness" => "Thickness", "endColor" => "End colour", _ => attribute
    };
}




