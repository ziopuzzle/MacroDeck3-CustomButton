using System.Xml.Linq;
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
        history.EndGroup();
        var heading = new TextBlock { Text = T(Types[node.Name.LocalName]) + " · " + selected, FontSize = 17, FontWeight = FontWeight.SemiBold };
        properties.Children.Add(heading);
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        discardPropertiesButton = Button("Discard incomplete input", DiscardPropertyInput, "discardProperties");
        discardPropertiesButton.IsEnabled = false;
        ToolTip.SetTip(discardPropertiesButton, "Discard incomplete fields only. Valid edits are kept; use Undo to revert applied changes.");
        tools.Children.Add(discardPropertiesButton); properties.Children.Add(tools);
        properties.Children.Add(new TextBlock { Text = T("Valid settings are applied automatically."), Foreground = B("#bbbbbb") });
        var attributes = new Dictionary<string, TextBox>(); TextBox? text = null;
        var type = node.Name.LocalName;
        if (type == "repeat") properties.Children.Add(new TextBlock { Text = "Repeat creates read-only local variables: item = current value (item.title for object fields), index = zero-based position, itemKey = stable key. These names shadow Channel data inside this Repeat only. Change the aliases below to avoid collisions; Channel data is not overwritten.", TextWrapping = TextWrapping.Wrap, Foreground = B("#bbbbbb") });
        var allowed = PropertyLayout.Attributes(type);
        attributes["id"] = Field(properties, PropertyDescriptions.Label("id"), (string?)node.Attribute("id") ?? "", "id", type);
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
            attributes["coordinates"] = Field(layout, PropertyDescriptions.Label("coordinates"), (string?)node.Attribute("coordinates") ?? "", "coordinates", type);
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
            attributes[attribute] = Field(panel, PropertyDescriptions.Label(attribute), (string?)node.Attribute(attribute) ?? "", attribute, node.Name.LocalName);
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
            foreach (var attribute in allowed.Where(a => a is not ("id" or "averageColorKey" or "averageColorFallback" or "itemAlias" or "indexAlias" or "keyAlias"))) fields[attribute] = Field(conditionGroups[PropertyLayout.Group(type, attribute)], PropertyDescriptions.Label(attribute), (string?)style.Attribute(attribute) ?? "", attribute, type, inherited: true);
            var index = rules.Count; panel.Children.Add(Button("Delete this condition", () => Edit(d => { d.DeleteStyle(selected, index); return selected; })));
            var header = new Expander { Header = $"{T("Condition")} {index + 1}: {(string?)style.Attribute("when")}", Content = panel, HorizontalAlignment = HorizontalAlignment.Stretch };
            properties.Children.Add(header); ruleHeaders.Add(header); rules.Add(fields);
        }
        updateDraftDescription = () =>
        {
            var draft = new XElement(node);
            if (text != null) { draft.Nodes().OfType<XText>().Remove(); draft.AddFirst(node.Name == "svg" ? new XCData(text.Text ?? "") : new XText(text.Text ?? "")); }
            SetDraftAttributes(draft, attributes);
            if (lineMode != null)
                foreach (var name in geometryFields.Keys.Where(name => !geometryNames[lineMode.SelectedIndex].Contains(name))) draft.SetAttributeValue(name, null);
            for (var i = 0; i < rules.Count; i++)
            {
                SetDraftAttributes(draft.Elements("style").ElementAt(i), rules[i]);
                ruleHeaders[i].Header = $"{T("Condition")} {i + 1}: {rules[i]["when"].Text}";
            }
            ApplyPropertyDraft(draft, heading, type);
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

    private static void SetDraftAttributes(XElement draft, IReadOnlyDictionary<string, TextBox> fields)
    {
        foreach (var pair in fields)
            draft.SetAttributeValue(pair.Key, string.IsNullOrWhiteSpace(pair.Value.Text) ? null : pair.Value.Text);
    }

    private void ApplyPropertyDraft(XElement draft, TextBlock heading, string type)
    {
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
            var renamed = selected != Id(draft);
            if (renamed)
            {
                if (collapsed.Remove(selected)) collapsed.Add(Id(draft));
                selected = Id(draft);
                heading.Text = T(Types[type]) + " · " + selected;
                var offset = scroll.Offset;
                targets.Clear(); rows.Children.Clear(); AddRows(new LayoutDocument(next).Root, 0);
                scroll.Offset = offset;
            }
            propertyDirty = false;
            UpdateHistoryButtons();
            rebuilding = true; xml.Text = history.Xml; rebuilding = false; xmlDirty = false;
            Publish();
            SayDraftApplied();
        }
        catch (Exception e) when (e is FormatException or System.Xml.XmlException or InvalidOperationException)
        { UpdateHistoryButtons(); Say("Input incomplete in '" + selected + "': " + e.Message + " The last valid display is kept."); }
    }
}
