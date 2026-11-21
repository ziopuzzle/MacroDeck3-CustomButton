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
    private Button undoButton = null!, redoButton = null!;
    private Button discardXmlButton = null!;
    private Button? discardPropertiesButton;
    private readonly Button showPreviewIssue;
    private readonly bool testing;
    private readonly Action<string>? sendDraft;
    private readonly string language;
    private string T(string value) => TextCatalog.Translate(value, language);
    private readonly StackPanel rows = new() { Spacing = 6 }, properties = new() { Spacing = 6 };
    private readonly ScrollViewer scroll;
    private readonly TextBox xml = new() { Name = "xml", AcceptsReturn = true, AcceptsTab = true, FontFamily = new FontFamily("monospace"), TextWrapping = TextWrapping.NoWrap };
    private readonly TextBlock status = new() { Name = "status", TextWrapping = TextWrapping.Wrap, Foreground = B("#bbbbbb") };
    private readonly ToggleButton guiTab = new() { Content = "GUI" }, xmlTab = new() { Content = "XML" };
    private readonly HashSet<string> collapsed = [];
    private readonly List<Row> targets = [];
    private readonly string[] keys;
    private readonly IReadOnlyDictionary<string, JsonElement> initialValues;
    private readonly Control guiPage, xmlPage;
    private readonly Grid canvas;
    private readonly Border indicator = new() { Name = "dropIndicator", IsVisible = false, IsHitTestVisible = false, BorderBrush = B("#2196f3"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private string selected, basis;
    private bool rebuilding, propertyDirty, xmlDirty, conflict, closingApproved, closingPromptOpen;
    private readonly DispatcherTimer publishTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private bool disconnected;
    private string? copiedXml;
    private EditorValidation.Issue? inputIssue;
    private string? dragId;
    private IPointer? dragPointer;
    private Point dragStart;
    private bool dragging;
    private Row? dropRow;
    private string? placement;
    public bool HostClosed { get; set; }
    public string LayoutXml => history.Xml;
    private sealed record Row(string Id, Border Card, bool Container, bool Root, bool Footer, TextBlock? Warning = null);
    private static readonly Dictionary<string, string> Types = new()
    {
        ["repeat"] = "↻ Repeat", ["scroll"] = "↕ Scroll", ["stack"] = "▤ Stack", ["layer"] = "▧ Layer", ["text"] = "T Text", ["bar"] = "━ Range bar",
        ["modifier"] = "▣ Modifier", ["responsive"] = "▦ Responsive", ["variant"] = "▥ Layout variant", ["dynamic-text"] = "◷ Dynamic text",
        ["transform"] = "↻ Transform", ["icon"] = "☆ Icon", ["gauge"] = "◴ Gauge",
        ["chart"] = "⌁ History graph", ["clock"] = "◷ Clock", ["progress-bar"] = "▷ Playback progress", ["slider"] = "● Slider", ["dial"] = "◉ Dial", ["trackpad"] = "⊕ Trackpad", ["text-field"] = "✎ Text field", ["toggle"] = "◐ Toggle", ["segmented"] = "▥ Segmented",
        ["rect"] = "▰ Rectangle", ["line"] = "╱ Line", ["polygon"] = "△ Polygon", ["sector"] = "◔ Sector",
        ["svg"] = "▧ SVG", ["image"] = "▧ Image", ["circle"] = "○ Circle", ["capsule"] = "▰ Capsule", ["path"] = "◇ Path"
    };
    private static IBrush B(string text) => Brush.Parse(text);
    private static string Id(XElement node) => (string)node.Attribute("id")!;
    private static string Colour(string type) => type switch { "repeat" or "scroll" or "stack" or "layer" or "transform" or "modifier" or "responsive" or "variant" or "segmented" => "#9874cf", "text" => "#50adf2", _ => "#48b5a1" };

    private readonly bool modal;
    public EditorWindow(string layout, string values, bool testing = false, Action<string>? sendDraft = null,
        IReadOnlyDictionary<string, JsonElement>? channelValues = null, bool modal = false,
        IReadOnlyDictionary<string, JsonElement>? sharedValues = null)
    {
        // English is currently the only supported native-editor language. No OS/user override.
        this.language = "en";
        this.testing = testing; history = new(layout); basis = layout; selected = Id(new LayoutDocument(layout).Root);
        this.sendDraft = sendDraft ?? (testing ? null : message => { Console.WriteLine(message); Console.Out.Flush(); });
        this.modal = modal;
        Dictionary<string, JsonElement> data;
        try { data = DataHub.ParseValues(values); }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException) { data = new(); }
        var snapshot = DataSnapshot.Empty with { Values = channelValues ?? DataSnapshot.Empty.Values,
            SharedValues = sharedValues ?? DataSnapshot.Empty.SharedValues };
        initialValues = snapshot.Resolve(data);
        keys = initialValues.Keys.ToArray();
        Title = T("Custom Button — Layout editor");
        Width = 1140; Height = 780; MinWidth = 900; MinHeight = 580;
        Background = B("#171717"); Foreground = B("#eeeeee"); FontSize = 13;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), Margin = new Thickness(18), RowSpacing = 12 };
        var top = new DockPanel(); var tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        tabs.Children.Add(CopyXmlButton("Copy XML", "copyXml"));
        tabs.Children.Add(guiTab); tabs.Children.Add(xmlTab); DockPanel.SetDock(tabs, Dock.Right); top.Children.Add(tabs);
        top.Children.Add(new TextBlock { Text = T("Layout"), FontSize = 22, FontWeight = FontWeight.SemiBold }); root.Children.Add(top);
        var content = new Grid(); Grid.SetRow(content, 1); root.Children.Add(content);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,10,340") };
        grid.ColumnDefinitions[0].MinWidth = 420; grid.ColumnDefinitions[2].MinWidth = 280;
        var middle = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 8 };
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        undoButton = Button("↶ Undo", () => Undo(false), "undo"); redoButton = Button("↷ Redo", () => Undo(true), "redo");
        toolbar.Children.Add(undoButton); toolbar.Children.Add(redoButton);
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
        xmlTools.Children.Add(Button("Format", FormatXml));
        discardXmlButton = Button("Discard XML changes", DiscardXmlInput, "discardXml");
        xmlTools.Children.Add(discardXmlButton);
        xmlTools.Children.Add(CopyXmlButton("Copy", "copyXmlDraft"));
        xmlGrid.Children.Add(xmlTools); Grid.SetRow(xml, 1); xmlGrid.Children.Add(xml); xmlPage = xmlGrid;
        content.Children.Add(guiPage); content.Children.Add(xmlPage);
        var footer = new DockPanel();
        showPreviewIssue = Button("Show in XML", ShowIssue, "showPreviewIssue");
        DockPanel.SetDock(showPreviewIssue, Dock.Right);
        footer.Children.Add(showPreviewIssue); footer.Children.Add(status);
        Grid.SetRow(footer, 2); root.Children.Add(footer); Content = root;
        xml.PropertyChanged += (_, e) =>
        {
            if (e.Property != TextBox.TextProperty || rebuilding) return;
            xmlDirty = xml.Text != history.Xml;
            inputIssue = null;
            Say(xmlDirty ? "Apply XML to validate your changes." : "XML matches the applied draft.");
            UpdateHistoryButtons();
        };
        guiTab.Click += (_, _) => ShowTab(false); xmlTab.Click += (_, _) => ShowTab(true);
        Closing += async (_, e) =>
        {
            FlushPublish();
            if (HostClosed || closingApproved || !propertyDirty && !xmlDirty && !NeedsRecovery) return;
            e.Cancel = true;
            if (closingPromptOpen) return;
            closingPromptOpen = true;
            try
            {
                if (propertyDirty || xmlDirty)
                {
                    var result = await ConfirmPending();
                    if (result == "apply") { if (!ResolvePending()) return; }
                    else if (result == "discard")
                    {
                        if (xmlDirty) DiscardXmlInput();
                        if (propertyDirty) DiscardPropertyInput();
                    }
                    else return;
                }
                FlushPublish();
                if (NeedsRecovery && !await ConfirmRecovery()) return;
                closingApproved = true; Close();
            }
            finally { closingPromptOpen = false; }
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
        RefreshHistoryPreviewWarning();
        Refresh(); ShowTab(false); Say("Add with ＋ and drag ⠿ to move. Preview and save in Macro Deck.");
    }
    private Button Button(string label, Action action, string? name = null)
    {
        var button = new Button { Content = T(label), Name = name, Padding = new Thickness(10, 5) };
        button.Click += (_, _) => Guard(action); return button;
    }
    private string? RecoveryNotice => conflict
        ? "The layout changed in Macro Deck. Further changes are kept in this editor and are not sent. Copy XML to recover them."
        : disconnected ? "Macro Deck disconnected. Further changes are kept in this editor and are not sent. Copy XML to recover them." : null;
    private bool NeedsRecovery => (conflict || disconnected && basis != history.Xml) && copiedXml != history.Xml;
    private EditorValidation.Issue? CurrentIssue => inputIssue ?? (xmlDirty ? null : previewWarning);
    private void Say(string text) => status.Text = T(string.Join("\n", new[] { RecoveryNotice,
        CurrentIssue is { } issue ? "⚠ " + issue.Message : null, text }.Where(s => !string.IsNullOrEmpty(s))));
    private void SayDraftApplied()
    {
        var applied = RecoveryNotice != null ? "Changes kept in this editor."
            : "Changes applied to the draft. Save in Macro Deck to finish.";
        Say(applied);
    }
    private Button CopyXmlButton(string label, string name)
    {
        var button = Button(label, () => { }, name);
        ToolTip.SetTip(button, "Copy the XML draft, including unapplied XML edits. Incomplete property input is not included.");
        button.Click += async (_, _) => await CopyXml();
        return button;
    }
    private async Task<bool> CopyXml()
    {
        if (Clipboard == null) { Say("Clipboard unavailable. Select and copy the text in the XML tab."); return false; }
        try
        {
            var text = xml.Text ?? history.Xml;
            await Clipboard.SetTextAsync(text);
            copiedXml = text;
            Say(propertyDirty ? "Last valid XML copied. Incomplete property input is not included."
                : xmlDirty ? "XML copied, including unapplied changes." : "XML copied.");
            return true;
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or NotSupportedException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        { Say("Could not copy XML: " + e.Message); return false; }
    }
    private void Guard(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is FormatException or System.Xml.XmlException or InvalidOperationException or IOException)
        {
            if (xmlDirty)
            {
                ReportXmlError(e);
                if (inputIssue is { Line: > 0 } issue) SelectXmlLocation(issue.Line, issue.Column, issue.Field == null ? null : issue.Length);
            }
            else Say("⚠ " + e.Message);
        }
    }
    private void ReportXmlError(Exception error)
    {
        inputIssue = EditorValidation.Describe(error);
        UpdateHistoryButtons();
        Say("Correct the input or select Discard XML changes.");
    }
    private void ShowIssue()
    {
        if (propertyDirty) { FocusIssueField(); return; }
        if (CurrentIssue is not { Line: > 0 } issue) return;
        if (!xmlDirty) ShowTab(true);
        SelectXmlLocation(issue.Line, issue.Column, xmlDirty && issue.Field == null ? null : issue.Length);
    }
    private void SelectXmlLocation(int line, int column, int? length = null)
    {
        var text = xml.Text ?? "";
        var offset = EditorValidation.Offset(text, line, column);
        var end = length.HasValue ? Math.Min(text.Length, offset + length.Value) : text.IndexOfAny(['\r', '\n'], offset);
        xml.Focus(); xml.CaretIndex = offset; xml.SelectionStart = offset;
        xml.SelectionEnd = end < 0 ? text.Length : end;
    }
    private Button AddButton(Func<string> target)
    {
        var button = Button("＋ Add component", () => { });
        button.Click += (_, _) =>
        {
            var menu = new ContextMenu();
            foreach (var (label, types) in new[] {
                ("Layout", new[] { "stack", "layer", "scroll", "repeat", "modifier", "transform", "responsive", "variant" }),
                ("Text and time", new[] { "text", "dynamic-text", "clock" }),
                ("Images", new[] { "icon", "image", "svg" }),
                ("Shapes", new[] { "line", "rect", "circle", "sector", "capsule", "polygon", "path" }),
                ("Data display", new[] { "bar", "gauge", "chart", "progress-bar" }),
                ("Controls", new[] { "toggle", "slider", "dial", "segmented", "text-field", "trackpad" }) })
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
        if (propertyDirty) { Say("Correct the input or select Discard incomplete input."); return false; }
        try { if (xmlDirty) ApplyXml(); return true; }
        catch (Exception e) when (e is FormatException or System.Xml.XmlException or InvalidOperationException)
        { ReportXmlError(e); return false; }
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
    private async Task<bool> ConfirmRecovery()
    {
        var dialog = new Window { Title = "Recover unsent changes", Width = 560, Height = 230, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 16 };
        var message = new TextBlock { Text = "These changes could not be sent to Macro Deck. Copy the XML before closing, or explicitly discard this editor's copy.", TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(message);
        var choices = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var copy = Button("Copy XML and close", () => { }, "copyRecovery");
        copy.Click += async (_, _) =>
        {
            if (await CopyXml()) dialog.Close(true);
            else message.Text = "XML could not be copied. Cancel to keep the editor open and copy the text manually.";
        };
        choices.Children.Add(copy);
        choices.Children.Add(Button("Discard and close", () => dialog.Close(true), "discardRecovery"));
        choices.Children.Add(Button("Cancel", () => dialog.Close(false), "cancelRecovery"));
        panel.Children.Add(choices); dialog.Content = panel;
        return await dialog.ShowDialog<bool>(this);
    }
    private EditorValidation.Issue? previewWarning;
    private string? lastPreviewableXml;
    private void CheckPreview(string layout)
    {
        var issue = EditorValidation.Check(layout, initialValues, lastPreviewableXml, modal);
        if (issue == null) lastPreviewableXml = layout;
        else if (previewWarning == null) history.EndGroup();
        previewWarning = issue;
    }
    private void ApplyXml() { CheckPreview(xml.Text ?? ""); history.Set(xml.Text ?? ""); inputIssue = null; xmlDirty = propertyDirty = false; Publish(); Refresh(); SayDraftApplied(); }
    private void FormatXml()
    {
        xml.Text = new LayoutDocument(xml.Text ?? "").Serialize();
        Say(xmlDirty ? "XML formatted. Apply XML to update the layout draft." : "XML formatting is already up to date.");
    }
    private void DiscardXmlInput()
    {
        if (!xmlDirty) { RevertPreviewEdit(); return; }
        inputIssue = null;
        rebuilding = true; xml.Text = history.Xml; rebuilding = false; xmlDirty = false;
        UpdateHistoryButtons();
        Say("Unapplied XML changes discarded. The last applied layout is kept.");
    }
    private void DiscardPropertyInput()
    {
        if (!propertyDirty) { RevertPreviewEdit(); return; }
        inputIssue = null; propertyDirty = false; Refresh();
        Say("Incomplete input discarded. The last applied layout is kept.");
    }
    private void RevertPreviewEdit()
    {
        if (previewWarning == null || !history.CanUndo) return;
        if (previewWarning.RevertedXml is not { } reverted) { Undo(false); return; }
        history.Set(reverted);
        RefreshHistoryPreviewWarning(); Publish(); Refresh(); SayDraftApplied();
    }
    private void Edit(Func<LayoutDocument, string> edit)
    {
        if (!ResolvePending()) return;
        selected = history.Edit(edit); RefreshHistoryPreviewWarning(); Publish(); Refresh(); SayDraftApplied();
    }
    private void RefreshHistoryPreviewWarning()
    {
        // History may contain a legacy layout that cannot render. Diagnose it without
        // preventing Undo/Redo from restoring the user's previous XML.
        try { CheckPreview(history.Xml); }
        catch (Exception e) when (e is FormatException or System.Xml.XmlException or InvalidOperationException)
        { previewWarning = EditorValidation.Describe(e) with { Message = "Cannot preview this layout: " + e.Message }; }
    }
    private void UpdateHistoryButtons()
    {
        undoButton.IsEnabled = history.CanUndo || propertyDirty;
        redoButton.IsEnabled = history.CanRedo && !propertyDirty;
        var canRevert = previewWarning != null && history.CanUndo;
        var revertLabel = previewWarning?.RevertedXml != null ? "Revert " + previewWarning.Field : "Undo last edit";
        discardXmlButton.IsEnabled = xmlDirty || canRevert;
        discardXmlButton.Content = !xmlDirty && canRevert ? revertLabel : "Discard XML changes";
        if (discardPropertiesButton != null)
        {
            discardPropertiesButton.IsEnabled = propertyDirty || canRevert;
            discardPropertiesButton.Content = !propertyDirty && canRevert ? revertLabel : "Discard incomplete input";
        }
        var issue = CurrentIssue;
        UpdateFieldWarning(issue);
        showPreviewIssue.Content = propertyDirty ? "Show field" : "Show in XML";
        showPreviewIssue.IsVisible = propertyDirty ? markedField != null : issue is { Line: > 0 };
        showPreviewIssue.IsEnabled = !xmlDirty || inputIssue != null;
        foreach (var row in targets)
        {
            if (row.Warning == null) continue;
            row.Warning.IsVisible = row.Id == issue?.Id;
            if (row.Warning.IsVisible) ToolTip.SetTip(row.Warning, issue!.Message);
        }
    }
    private void Undo(bool redo)
    {
        if (xmlDirty) { if (!ResolvePending()) return; }
        // Incomplete property input has not entered history. Undo discards it first.
        if (propertyDirty) { DiscardPropertyInput(); return; }
        if (redo) history.Redo(); else { lastPreviewableXml = null; history.Undo(); }
        RefreshHistoryPreviewWarning(); Publish(); Refresh(); SayDraftApplied();
    }
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
            basis = history.Xml;
            if (!propertyDirty && !xmlDirty) Say("Changes sent to the draft. Save in Macro Deck to finish.");
        }
        catch (IOException) { Disconnect(); }
    }
    public void Disconnect()
    {
        disconnected = true; publishTimer.Stop();
        // Keep both the current valid XML and any incomplete input available for recovery.
        Say("Your work is still here. Copy XML before closing, then reopen the editor from Macro Deck.");
        Title = T(conflict ? "Custom Button — Conflict (copy XML to recover)" : "Custom Button — Disconnected (copy XML to recover)");
    }
    public void Conflict()
    {
        conflict = true; publishTimer.Stop();
        Title = T("Custom Button — Conflict (copy XML to recover)");
        Say("Copy XML, then close and reopen the editor.");
    }
    private void Refresh()
    {
        rebuilding = true; var offset = scroll.Offset;
        var doc = new LayoutDocument(history.Xml);
        if (!doc.Components.Any(n => Id(n) == selected)) selected = Id(doc.Root);
        foreach (var ancestor in doc.Find(selected).Ancestors()) collapsed.Remove(Id(ancestor));
        targets.Clear(); rows.Children.Clear(); AddRows(doc.Root, 0);
        xml.Text = history.Xml; xmlDirty = false; rebuilding = false; MakeProperties();
        UpdateHistoryButtons();
        Dispatcher.UIThread.Post(() => scroll.Offset = offset, DispatcherPriority.Loaded);
    }
    private void Select(string id)
    {
        if (selected == id || !ResolvePending()) return;
        selected = id; MakeProperties();
        UpdateHistoryButtons();
        foreach (var row in targets) if (!row.Footer) row.Card.Background = B(row.Id == selected ? "#26323b" : "#202020");
    }
}

