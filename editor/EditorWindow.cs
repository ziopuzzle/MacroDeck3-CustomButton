using System.Text.Json;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Ziopuzzle.CustomButton;

public sealed partial class EditorWindow : Window
{
    private readonly LayoutEditHistory history;
    private readonly bool testing;
    private readonly Action<string>? sendDraft;
    private readonly string language;
    private string T(string value) => TextCatalog.Translate(value, language);
    private readonly StackPanel rows = new() { Spacing = 6 }, properties = new() { Spacing = 6 };
    private readonly ScrollViewer scroll;
    private readonly TextBox xml = new() { Name = "xml", AcceptsReturn = true, AcceptsTab = true, FontFamily = new FontFamily("monospace"), TextWrapping = TextWrapping.NoWrap };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Foreground = B("#bbbbbb") };
    private readonly ToggleButton guiTab = new() { Content = "GUI" }, xmlTab = new() { Content = "XML" };
    private readonly HashSet<string> collapsed = [];
    private readonly List<Row> targets = [];
    private readonly string[] keys;
    private readonly IReadOnlyDictionary<string, JsonElement> initialValues;
    private readonly Control guiPage, xmlPage;
    private readonly Grid canvas;
    private readonly Border indicator = new() { Name = "dropIndicator", IsVisible = false, IsHitTestVisible = false, BorderBrush = B("#2196f3"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private string selected, basis;
    private bool rebuilding, propertyDirty, xmlDirty, conflict, closingApproved;
    private readonly DispatcherTimer publishTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private bool disconnected;
    private Action? applyProperties;
    private string? dragId;
    private IPointer? dragPointer;
    private Point dragStart;
    private bool dragging;
    private Row? dropRow;
    private string? placement;
    public bool HostClosed { get; set; }
    public string LayoutXml => history.Xml;
    private sealed record Row(string Id, Border Card, bool Container, bool Root, bool Footer);
    private static readonly Dictionary<string, string> Types = new()
    {
        ["stack"] = "▤ Stack", ["layer"] = "▧ Layer", ["text"] = "T Text", ["bar"] = "━ Bar",
        ["chart"] = "⌁ History graph", ["clock"] = "◷ Clock", ["progress-bar"] = "▷ Playback progress", ["slider"] = "● Slider",
        ["rect"] = "▰ Rectangle", ["line"] = "╱ Line", ["polygon"] = "△ Polygon", ["sector"] = "◔ Sector",
        ["image"] = "▧ Image", ["circle"] = "○ Circle", ["capsule"] = "▰ Capsule", ["path"] = "◇ Path"
    };
    private static IBrush B(string text) => Brush.Parse(text);
    private static string Id(XElement node) => (string)node.Attribute("id")!;
    private static string Colour(string type) => type switch { "stack" or "layer" => "#9874cf", "text" => "#50adf2", _ => "#48b5a1" };

    public EditorWindow(string layout, string values, bool testing = false, Action<string>? sendDraft = null)
    {
        // English is currently the only supported native-editor language. No OS/user override.
        this.language = "en";
        this.testing = testing; history = new(layout); basis = layout; selected = Id(new LayoutDocument(layout).Root);
        this.sendDraft = sendDraft ?? (testing ? null : message => { Console.WriteLine(message); Console.Out.Flush(); });
        try { initialValues = DataHub.ParseValues(values); keys = initialValues.Keys.ToArray(); }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException) { initialValues = new Dictionary<string, JsonElement>(); keys = []; }
        Title = T("Custom Button — Layout editor");
        Width = 1140; Height = 780; MinWidth = 900; MinHeight = 580;
        Background = B("#171717"); Foreground = B("#eeeeee"); FontSize = 13;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), Margin = new Thickness(18), RowSpacing = 12 };
        var top = new DockPanel(); var tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        tabs.Children.Add(guiTab); tabs.Children.Add(xmlTab); DockPanel.SetDock(tabs, Dock.Right); top.Children.Add(tabs);
        top.Children.Add(new TextBlock { Text = T("Layout"), FontSize = 22, FontWeight = FontWeight.SemiBold }); root.Children.Add(top);
        var content = new Grid(); Grid.SetRow(content, 1); root.Children.Add(content);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,10,340") };
        grid.ColumnDefinitions[0].MinWidth = 420; grid.ColumnDefinitions[2].MinWidth = 280;
        var middle = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 8 };
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        toolbar.Children.Add(Button("↶ Undo", () => Undo(false), "undo")); toolbar.Children.Add(Button("↷ Redo", () => Undo(true), "redo"));
        toolbar.Children.Add(AddButton(() => selected)); middle.Children.Add(toolbar);
        scroll = new ScrollViewer { Name = "blocksScroll", Content = rows, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(10) };
        canvas = new Grid { ClipToBounds = true }; canvas.Children.Add(scroll); canvas.Children.Add(indicator);
        var frame = new Border { Child = canvas, Background = B("#1c1c1c"), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), BorderBrush = B("#363636") };
        Grid.SetRow(frame, 1); middle.Children.Add(frame); grid.Children.Add(middle);
        var splitter = new GridSplitter { Width = 4, Background = B("#363636"), ResizeDirection = GridResizeDirection.Columns, ResizeBehavior = GridResizeBehavior.PreviousAndNext };
        Grid.SetColumn(splitter, 1); grid.Children.Add(splitter);
        properties.Margin = new Thickness(0, 0, 20, 0);
        var propertyScroll = new ScrollViewer { Name = "propertiesScroll", Content = properties, Padding = new Thickness(12, 0, 0, 0), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Visible };
        Grid.SetColumn(propertyScroll, 2); grid.Children.Add(propertyScroll); guiPage = grid;
        var xmlGrid = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 8 };
        var xmlTools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        xmlTools.Children.Add(Button("Apply XML", ApplyXml));
        xmlTools.Children.Add(Button("Format", () => { history.Set(new LayoutDocument(xml.Text ?? "").Serialize()); propertyDirty = xmlDirty = false; Publish(); Refresh(); }));
        var copy = Button("Copy", () => { }); copy.Click += async (_, _) => { if (Clipboard != null) await Clipboard.SetTextAsync(xml.Text); }; xmlTools.Children.Add(copy);
        xmlGrid.Children.Add(xmlTools); Grid.SetRow(xml, 1); xmlGrid.Children.Add(xml); xmlPage = xmlGrid;
        content.Children.Add(guiPage); content.Children.Add(xmlPage); Grid.SetRow(status, 2); root.Children.Add(status); Content = root;
        xml.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty && !rebuilding) xmlDirty = true; };
        guiTab.Click += (_, _) => ShowTab(false); xmlTab.Click += (_, _) => ShowTab(true);
        Closing += async (_, e) =>
        {
            FlushPublish();
            if (HostClosed || closingApproved || !propertyDirty && !xmlDirty) return;
            e.Cancel = true;
            var result = await ConfirmPending();
            if (result == "discard" || result == "apply" && ResolvePending()) { closingApproved = true; Close(); }
        };
        publishTimer.Tick += (_, _) => FlushPublish();
        Closed += (_, _) => publishTimer.Stop();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && dragId != null) { EndDrag(); e.Handled = true; return; }
            if (e.Source is TextBox || (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) == 0) return;
            if (e.Key == Key.Z) { Guard(() => Undo((e.KeyModifiers & KeyModifiers.Shift) != 0)); e.Handled = true; }
            else if (e.Key == Key.Y) { Guard(() => Undo(true)); e.Handled = true; }
        };
        Refresh(); ShowTab(false); Say("Add with ＋ and drag ⠿ to move. Preview and save in Macro Deck.");
    }
    private Button Button(string label, Action action, string? name = null)
    {
        var button = new Button { Content = T(label), Name = name, Padding = new Thickness(10, 5) };
        button.Click += (_, _) => Guard(action); return button;
    }
    private void Say(string text) => status.Text = T(text);
    private void Guard(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is FormatException or System.Xml.XmlException or InvalidOperationException or IOException) { Say(e.Message); }
    }
    private Button AddButton(Func<string> target)
    {
        var button = Button("＋ Add component", () => { });
        button.Click += (_, _) =>
        {
            var menu = new ContextMenu();
            foreach (var (label, types) in new[] {
                ("Layout", new[] { "stack", "layer" }),
                ("Text and images", new[] { "text", "image" }),
                ("Data and controls", new[] { "bar", "chart", "clock", "progress-bar", "slider" }),
                ("Shapes", new[] { "rect", "circle", "capsule", "line", "polygon", "sector", "path" }) })
            {
                var category = new MenuItem { Header = T(label) };
                foreach (var type in types)
                {
                    var entry = new MenuItem { Header = T(Types[type]) };
                    entry.Click += (_, _) => Guard(() => Edit(d => d.Add(target(), type))); category.Items.Add(entry);
                }
                menu.Items.Add(category);
            }
            button.ContextMenu = menu; menu.Open(button);
        };
        return button;
    }
    private void ShowTab(bool showXml)
    {
        if (ResolvePending()) { guiPage.IsVisible = !showXml; xmlPage.IsVisible = showXml; }
        guiTab.IsChecked = guiPage.IsVisible; xmlTab.IsChecked = xmlPage.IsVisible;
    }
    private bool ResolvePending()
    {
        try { if (xmlDirty) ApplyXml(); if (propertyDirty) applyProperties?.Invoke(); return !propertyDirty && !xmlDirty; }
        catch (Exception e) when (e is FormatException or System.Xml.XmlException or InvalidOperationException) { Say("Correct the input or select Reset input: " + e.Message); return false; }
    }
    private async Task<string?> ConfirmPending()
    {
        var dialog = new Window { Title = T("Unapplied input"), Width = 440, Height = 180, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 16 };
        panel.Children.Add(new TextBlock { Text = T("There is unapplied input. Apply it before closing?"), TextWrapping = TextWrapping.Wrap });
        var choices = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var (label, value) in new[] { ("Apply", "apply"), ("Discard", "discard"), ("Cancel", "cancel") }) choices.Children.Add(Button(label, () => dialog.Close(value)));
        panel.Children.Add(choices); dialog.Content = panel; return await dialog.ShowDialog<string?>(this);
    }
    private void ApplyXml() { history.Set(xml.Text ?? ""); xmlDirty = propertyDirty = false; Publish(); Refresh(); }
    private void Edit(Func<LayoutDocument, string> edit)
    {
        if (!ResolvePending()) return;
        selected = history.Edit(edit); Publish(); Refresh();
    }
    private void Undo(bool redo) { if (!ResolvePending()) return; if (redo) history.Redo(); else history.Undo(); Publish(); Refresh(); }
    private void Publish()
    {
        if (sendDraft == null) return;
        // Coalesce high-frequency picker changes into at most four draft updates per second.
        if (!publishTimer.IsEnabled) publishTimer.Start();
    }
    private void FlushPublish()
    {
        publishTimer.Stop();
        if (sendDraft == null || disconnected) return;
        if (conflict) { Say("Conflicting changes detected. Copy the XML, then close and reopen the editor."); return; }
        if (basis == history.Xml) return;
        try
        {
            sendDraft(JsonSerializer.Serialize(new { layout = history.Xml, basis }));
            basis = history.Xml; Say("Changes sent to the draft. Save in Macro Deck to finish.");
        }
        catch (IOException) { Disconnect(); }
    }
    public void Disconnect()
    {
        disconnected = true; publishTimer.Stop();
        // Keep both the current valid XML and any incomplete input available for recovery.
        Say("Macro Deck disconnected. Your work is still here. Copy XML before closing, then reopen the editor from Macro Deck.");
        Title = T("Custom Button — Disconnected (copy XML to recover)");
    }
    public void Conflict() { conflict = true; Say("The layout was changed in Macro Deck. Copy the XML, then close and reopen the editor."); }
    private void Refresh()
    {
        rebuilding = true; var offset = scroll.Offset;
        var doc = new LayoutDocument(history.Xml);
        if (!doc.Components.Any(n => Id(n) == selected)) selected = Id(doc.Root);
        foreach (var ancestor in doc.Find(selected).Ancestors()) collapsed.Remove(Id(ancestor));
        targets.Clear(); rows.Children.Clear(); AddRows(doc.Root, 0);
        xml.Text = history.Xml; xmlDirty = false; rebuilding = false; MakeProperties();
        Dispatcher.UIThread.Post(() => scroll.Offset = offset, DispatcherPriority.Loaded);
    }
    private void Select(string id)
    {
        if (selected == id || !ResolvePending()) return;
        selected = id; MakeProperties();
        foreach (var row in targets) if (!row.Footer) row.Card.Background = B(row.Id == selected ? "#26323b" : "#202020");
    }
}

