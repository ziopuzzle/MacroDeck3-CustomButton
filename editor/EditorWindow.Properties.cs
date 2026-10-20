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
    private string propertyGroup = "";
    private readonly Dictionary<string, bool> propertyExpansion = new();
    private void MakeProperties()
    {
        updateDraftDescription = null;
        properties.Children.Clear(); propertyDirty = false;
        var node = new LayoutDocument(history.Xml).Find(selected);
        var originalXml = history.Xml;
        history.EndGroup();
        var heading = new TextBlock { Text = T(Types[node.Name.LocalName]) + " · " + selected, FontSize = 17, FontWeight = FontWeight.SemiBold };
        properties.Children.Add(heading);
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        tools.Children.Add(Button("Reset input", () => { history.Set(originalXml); propertyDirty = false; Publish(); Refresh(); })); properties.Children.Add(tools);
        properties.Children.Add(new TextBlock { Text = T("Valid settings are applied automatically."), Foreground = B("#bbbbbb") });
        var attributes = new Dictionary<string, TextBox>(); TextBox? text = null;
        var type = node.Name.LocalName;
        if (type == "repeat") properties.Children.Add(new TextBlock { Text = "Repeat creates read-only local variables: item = current value (item.title for object fields), index = zero-based position, itemKey = stable key. These names shadow Channel data inside this Repeat only. Change the aliases below to avoid collisions; Channel data is not overwritten.", TextWrapping = TextWrapping.Wrap, Foreground = B("#bbbbbb") });
        var allowed = PropertyLayout.Attributes(type);
        attributes["id"] = Field(properties, Label("id"), (string?)node.Attribute("id") ?? "", "id", type);
        Dictionary<string, StackPanel> MakeGroups(StackPanel parent, bool condition = false)
        {
            var result = new Dictionary<string, StackPanel>();
            foreach (var group in PropertyLayout.Groups)
            {
                if (!allowed.Any(a => PropertyLayout.Group(type, a) == group) && !(!condition && group == "Content and data" && type is "text" or "svg")) continue;
                var panel = new StackPanel { Spacing = 6 }; result[group] = panel;
                var expansionKey = type + ":" + group;
                var section = new Expander { Name = condition ? null : "group_" + group.Replace(' ', '_'), Header = T(group), Content = panel,
                    IsExpanded = !condition && propertyExpansion.TryGetValue(expansionKey, out var saved) ? saved : true, HorizontalAlignment = HorizontalAlignment.Stretch };
                if (!condition) section.PropertyChanged += (_, e) => { if (e.Property == Expander.IsExpandedProperty) propertyExpansion[expansionKey] = section.IsExpanded; };
                parent.Children.Add(section);
            }
            return result;
        }
        var groups = MakeGroups(properties);
        if (node.Name == "text" || node.Name == "svg") text = Field(groups["Content and data"], node.Name == "svg" ? "SVG markup" : "Display text", string.Concat(node.Nodes().OfType<XText>().Select(n => n.Value)), "text", type);
        var geometryNames = new[] { new[] { "x1", "y1", "x2", "y2" }, new[] { "cx", "cy", "length", "angle" }, new[] { "x", "y", "length", "direction" } };
        var geometry = new StackPanel { Spacing = 6 };
        ComboBox? lineMode = null;
        if (node.Name == "line")
        {
            lineMode = new ComboBox { Name = "lineMode", ItemsSource = new[] { "Two endpoints", "Start + length + angle", "Horizontal / vertical" },
                SelectedIndex = node.Attribute("angle") != null ? 1 : node.Attributes().Any(a => geometryNames[0].Contains(a.Name.LocalName)) ? 0 : 2,
                HorizontalAlignment = HorizontalAlignment.Stretch };
            var layout = groups["Layout and geometry"];
            attributes["coordinates"] = Field(layout, Label("coordinates"), (string?)node.Attribute("coordinates") ?? "", "coordinates", type);
            layout.Children.Add(new TextBlock { Text = "Line geometry" });
            layout.Children.Add(lineMode); layout.Children.Add(geometry);
        }
        var geometryFields = new Dictionary<string, StackPanel>();
        foreach (var attribute in allowed.Where(a => a != "id" && !(lineMode != null && a == "coordinates")))
        {
            var panel = groups[PropertyLayout.Group(type, attribute)];
            if (lineMode != null && geometryNames.Any(names => names.Contains(attribute)))
            {
                panel = new StackPanel { Spacing = 6 }; geometry.Children.Add(panel); geometryFields[attribute] = panel;
            }
            attributes[attribute] = Field(panel, Label(attribute), (string?)node.Attribute(attribute) ?? "", attribute, node.Name.LocalName);
        }
        void ShowGeometry()
        {
            if (lineMode == null) return;
            geometry.Children.Clear();
            foreach (var name in geometryNames[lineMode.SelectedIndex]) geometry.Children.Add(geometryFields[name]);
        }
        ShowGeometry();
        var rules = new List<Dictionary<string, TextBox>>();
        var ruleHeaders = new List<Expander>();
        foreach (var style in node.Elements("style"))
        {
            var fields = new Dictionary<string, TextBox>(); var panel = new StackPanel { Spacing = 6 };
            fields["when"] = Field(panel, "Condition expression", (string?)style.Attribute("when") ?? "", "when", node.Name.LocalName);
            var conditionGroups = MakeGroups(panel, condition: true);
            foreach (var attribute in allowed.Where(a => a is not ("id" or "averageColorKey" or "averageColorFallback" or "itemAlias" or "indexAlias" or "keyAlias"))) fields[attribute] = Field(conditionGroups[PropertyLayout.Group(type, attribute)], Label(attribute), (string?)style.Attribute(attribute) ?? "", attribute, type, inherited: true);
            var index = rules.Count; panel.Children.Add(Button("Delete this condition", () => Edit(d => { d.DeleteStyle(selected, index); return selected; })));
            var header = new Expander { Header = $"{T("Condition")} {index + 1}: {(string?)style.Attribute("when")}", Content = panel, HorizontalAlignment = HorizontalAlignment.Stretch };
            properties.Children.Add(header); ruleHeaders.Add(header); rules.Add(fields);
        }
        updateDraftDescription = () =>
        {
            var draft = new XElement(node);
            if (text != null) { draft.Nodes().OfType<XText>().Remove(); draft.AddFirst(node.Name == "svg" ? new XCData(text.Text ?? "") : new XText(text.Text ?? "")); }
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
                previewWarning = EditorValidation.Check(next, initialValues);
                if (next != history.Xml) history.SetGrouped(next, propertyGroup);
                UpdateHistoryButtons();
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
                Say(previewWarning ?? "Changes applied to the draft. Save in Macro Deck to finish.");
            }
            catch (Exception e) when (e is FormatException or System.Xml.XmlException or InvalidOperationException)
            { UpdateHistoryButtons(); Say("Input incomplete in '" + selected + "': " + e.Message + " The last valid display is kept."); }
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
            rebuilding = false; ShowGeometry(); propertyGroup = "lineMode"; propertyDirty = true; updateDraftDescription();
        };
        updateDraftDescription();
    }
    private TextBox Field(StackPanel panel, string label, string value, string attribute, string component, bool inherited = false)
    {
        panel.Children.Add(new TextBlock { Text = T(label), Foreground = B("#bbbbbb") });
        var row = new DockPanel { LastChildFill = true };
        var box = new TextBox { Name = "field_" + attribute, Text = value, Watermark = inherited ? null : PropertyDefaults.Get(component, attribute), MinWidth = 40, Padding = new Thickness(7, 5) };
        if (component == "svg" && attribute == "text")
        {
            box.AcceptsReturn = true; box.TextWrapping = TextWrapping.Wrap;
            box.MinHeight = 180; box.MaxHeight = 420;
        }
        ToolTip.SetTip(box, T("Leave blank for the default. Use {{dataName}}, {{= used / total * 100:F1}}%, or {{= floor(position / 1000):duration}} for milliseconds. Math supports floor, ceil, round, abs, min, max, clamp and more."));
        if (attribute == "id") ToolTip.SetTip(box, "Unique element ID. Valid edits apply automatically. Update matching action-event filters separately.");
        if (attribute == "anchor" && component == "scroll") ToolTip.SetTip(box, "Vertical scroll only. end follows the bottom while pinned; manually scrolling away suspends following. start disables following; it is not a command to jump to the top. Requires List version 3. Supports bindings and styles.");
        if (attribute == "followKey") ToolTip.SetTip(box, "Experimental: display data name containing a zero-based Repeat index. On change, temporarily removes rows and follows the target plus offset. Requires one direct Repeat child. Overrides anchor while enabled; missing/negative values disable following.");
        if (attribute == "followOffset") ToolTip.SetTip(box, "Item offset from the target index. 0 aligns the target at the bottom; 1 includes the next item. This is not a pixel offset or guaranteed centering.");
        if (attribute == "followDelayMs") ToolTip.SetTip(box, "Experimental phase delay, 0–5000 ms (default 300). Zero adds no intentional wait, but updates still use the session refresh cadence and transport delivery. Short delays can miss scrolling; increase if needed.");
        if (attribute is "followKey" or "followOffset" or "followDelayMs")
            ToolTip.SetTip(box, ToolTip.GetTip(box) + " Experimental: behavior and configuration may change in future versions.");
        if (attribute is "itemAlias" or "indexAlias" or "keyAlias") ToolTip.SetTip(box, "Literal local variable name; no braces or functions. Defaults: item, index, itemKey. Must be distinct. These read-only names shadow same-named Channel values only inside Repeat.");
        if (attribute == "items") ToolTip.SetTip(box, "Array data name or {{tracks}}. Children are rendered once per item. Use {{item.title}}, {{item}} and {{index}} inside them. Missing or null arrays render no children.");
        if (attribute == "itemKey") ToolTip.SetTip(box, "Optional literal property path such as id. Values must be unique, nonempty strings or numbers. Keeps item identity when reordered. Blank uses the zero-based index.");
        if (attribute == "color" && component is "image" or "svg") ToolTip.SetTip(box, "Optional single-colour tint. Preserves the image alpha mask and replaces all RGB colours. Leave blank for original colours. Supports bindings, conditions and colour transitions. Colour alpha multiplies opacity. Requires client mask support; tinted artwork does not crossfade on beta.15.");
        if (attribute == "rasterSize") ToolTip.SetTip(box, "SVG image's longest edge in pixels: 64–1024, default 512. The aspect ratio is preserved. SVG variables update with display data; unchanged SVG is cached.");
        if (attribute == "frame") ToolTip.SetTip(box, "square: use size for a square frame (default). container: fit the parent's allocated rectangle; size is ignored and fill defaults to true. Background stays transparent. In a scrolling layout, give the image a mainSize or an enclosing modifier a height.");
        if (attribute == "size" && component is "image" or "svg") ToolTip.SetTip(box, "Edge of the square image frame. Ignored when Image frame is container; use the parent's layout or mainSize instead.");
        if (attribute == "averageColorKey") ToolTip.SetTip(box, "Optional unique literal output name, read with {{name}}. Live widgets/modals also write it to Channel data for variable-write and copy actions. Preview edits stay local. Supports files, HTTP images, inline SVG and artwork, but not Icon Packs.");
        if (attribute == "averageColorFallback") ToolTip.SetTip(box, "Literal color used before a result is available; default transparent. Alpha is supported. Source-load failures retain the last successful image and color.");
        if (attribute == "transitionMs") ToolTip.SetTip(box, "0 disables transitions. Range: 0–10000 ms. Only later changes animate; the initial value appears immediately.");
        if (attribute == "angle") ToolTip.SetTip(box, "Degrees: 0 points right, 90 down. Use cx/cy/length; clear x1/y1/x2/y2 and x/y/direction. Animate angle to rotate a needle without shortening it. Use a square box for a circular sweep.");
        if (attribute is "offsetX" or "offsetY") ToolTip.SetTip(box, "Use a fraction from -1 to 1: 0.2 moves by 20% of the image box. Positive X moves right; positive Y moves down. The offset is applied after zoom. Supports data bindings and transitions.");
        if (attribute == "zoom") ToolTip.SetTip(box, "Multiplier after contain/cover fitting. 1 is normal; 1.5 is 150%. Range: 0.1–4. Supports data bindings and transitions.");
        if (attribute == "fontFace") ToolTip.SetTip(box, "Macro Deck font catalogue ID, not a Windows font name or file path. Use {{fontId}} for display data. Leave blank for the default font; unavailable IDs fall back to the client's default. Copy the ID from a standard button's saved font settings.");
        if (attribute == "source")
        {
            ToolTip.SetTip(box, "An absolute file path, HTTP/HTTPS URL, Macro Deck /api/... URL, artwork:/api/music-player/artwork/..., or icon-pack:UUID. Copy the UUID from icon.reference in a standard button's saved JSON. Use {{imageUrl}} for changing artwork. PNG, JPEG, WebP and GIF, up to 2 MiB. Blank hides the image.");
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
        if (attribute.EndsWith("color", StringComparison.OrdinalIgnoreCase) || attribute is "background" or "averageColorFallback")
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
        void Menu(string title, string tooltip, IEnumerable<(string Label, string Value)> entries)
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
        var options = LayoutOptions.Choices(component, attribute);
        if (options.Length > 0) Menu("▾", "Suggested values", new[] { ("Use default", "") }.Concat(options.Select(o => (o, o))));
        if (ExpressionInsertion.KeyOnly(attribute))
            Menu("▾", "Select a display data name (replaces the field; no value binding)", new[] { ("Clear", "") }.Concat(keys.Concat(new LayoutRenderer(history.Xml).ImageColorDefaults.Keys)
                .Concat(attribute == "items" ? LayoutRenderer.RepeatPreviewKeys(history.Xml, selected, initialValues) : []).Distinct().OrderBy(k => k).Select(k => (k, k))));
        else if (ExpressionInsertion.Available(attribute))
        {
            var insert = Button("{ }", () => { }, "insert_" + attribute);
            insert.Focusable = false; // Keep the text selection until the picker has captured it.
            ToolTip.SetTip(insert, "Insert display data or function. Search and read descriptions before inserting.");
            insert.Margin = new Thickness(4, 0, 0, 0);
            insert.Click += async (_, _) => { try { await InsertExpression(box, attribute); } catch (FormatException e) { Say(e.Message); } };
            DockPanel.SetDock(insert, Dock.Right); row.Children.Add(insert);
        }
        var groupKey = Guid.NewGuid().ToString();
        box.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty && !rebuilding) { propertyGroup = groupKey; propertyDirty = true; updateDraftDescription?.Invoke(); } }; row.Children.Add(box); panel.Children.Add(row); return box;
    }
    private static string Label(string attribute) => attribute switch
    {
        "id" => "Element ID",
        "followKey" => "Follow data key (experimental)",
        "followOffset" => "Follow item offset (experimental)",
        "followDelayMs" => "Follow delay (ms) (experimental)",
        "closeModal" => "Close modal on press (no press event)",
        "result" => "Modal result (requires Close modal)",
        "start" => "Range start (same units as value)", "marker" => "Marker (same units as value)",
        "items" => "Array source", "itemKey" => "Item key property", "itemAlias" => "Item variable name", "indexAlias" => "Index variable name", "keyAlias" => "Item key variable name",
        "format" => "Time / date format", "zone" => "Time zone (IANA name)", "clip" => "Clip shape", "disabled" => "Disable child interactions",
        "minWidth" => "Minimum width (variant: cells; modifier: length)", "maxWidth" => "Maximum width (variant: cells; modifier: length)",
        "minHeight" => "Minimum height (variant: cells; modifier: length)", "maxHeight" => "Maximum height (variant: cells; modifier: length)",
        "minAspect" => "Minimum width / height ratio", "maxAspect" => "Maximum width / height ratio",
        "name" => "Built-in icon name", "rotation" => "Rotation (clockwise degrees)",
        "originX" => "Pivot X (fraction of own width)", "originY" => "Pivot Y (fraction of own height)",
        "endAngle" => "End angle (degrees)",
        "angle" => "Line angle (degrees)",
        "fit" => "Image fit (contain / cover)",
        "frame" => "Image frame (square / container)",
        "zoom" => "Zoom (1 = normal)", "offsetX" => "Horizontal offset (fraction of width)", "offsetY" => "Vertical offset (fraction of height)",
        "source" => "Image source (file / URL / Icon Pack)", "averageColorKey" => "Average color output name", "averageColorFallback" => "Average color fallback", "transition" => "Image change transition", "brightness" => "Brightness (0–2)", "saturation" => "Saturation (0–2)",
        "coordinates" => "Coordinates (widget basis / local box)",
        "fontFace" => "Font (Macro Deck catalogue ID)", "weight" => "Font weight (regular / medium / semibold / bold)",
        "x1" => "Start X (0–100%)", "y1" => "Start Y (0–100%)", "x2" => "End X (0–100%)", "y2" => "End Y (0–100%)",
        "gradient" => "Fill gradient", "gradientAngle" => "Gradient angle (0 up, 90 right)", "gradientX" => "Radial center X (0–100%)", "gradientY" => "Radial center Y (0–100%)",
        "transitionMs" => "Transition duration (ms)", "transitionProperties" => "Transition properties (space separated)", "easing" => "Transition easing", "colorSpace" => "Color interpolation space",
        "keyX" => "X data key", "keyY" => "Y data key", "leftValue" => "Left edge value", "rightValue" => "Right edge value", "topValue" => "Top edge value", "bottomValue" => "Bottom edge value", "stepX" => "X step (0 = continuous)", "stepY" => "Y step (0 = continuous)",
        "showCursorWhileTouching" => "Show cursor only while touching", "interactive" => "Enable interaction", "step" => "Value step", "key" => "Display data key",
        "shadow" => "Text shadow", "trackColor" => "Track colour", "opacity" => "Opacity (0–1)", "strokeColor" => "Stroke color", "strokeWidth" => "Stroke width", "cornerRadius" => "Corner radius", "data" => "Path (absolute coordinates)",
        "cx" => "Center X", "cy" => "Center Y", "radius" => "Radius", "startAngle" => "Start angle (degrees)", "sweepAngle" => "Sweep angle (degrees)", "points" => "Point count / polygon coordinates (x,y; x,y; …)",
        "x" => "Position from left", "y" => "Position from top", "width" => "Width", "height" => "Height", "length" => "Line length", "corner" => "Corners (square / rounded)",
        "direction" => "Direction", "color" => "Colour", "background" => "Background colour", "size" => "Size (e.g. 18%)", "mainSize" => "Main-axis size", "fill" => "Fill remaining space", "visible" => "Visible", "visibleWhen" => "Visibility condition", "padding" => "Padding", "gap" => "Gap between components", "align" => "Cross-axis alignment", "justify" => "Main-axis alignment", "borderStyle" => "Border style", "borderColor" => "Border colour", "value" => "Display value", "min" => "Minimum", "max" => "Maximum", "thickness" => "Thickness", "endColor" => "End colour", _ => attribute
    };
}



