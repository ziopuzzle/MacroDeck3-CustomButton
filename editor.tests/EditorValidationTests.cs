using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Editor.Tests;

public class EditorValidationTests
{
    private const string Original = "<text id='title'>Original</text>";

    [AvaloniaTest]
    public void InvalidSliderMinimumStaysMarkedAndReachableAfterBlockedActions()
    {
        const string original = "<slider id='volume' min='0'/>";
        var window = new EditorWindow(original, "{}", true);
        window.Show(); Flush(window);
        try
        {
            var min = Find<TextBox>(window, "field_min");
            min.Text = "0a"; Flush(window);
            Assert.That(Find<TextBlock>(window, "warning_volume").IsVisible, Is.True);
            Assert.That(Find<TextBlock>(window, "label_min").Text, Does.StartWith("⚠"));
            Assert.That(Find<TextBlock>(window, "label_max").Text, Does.Not.StartWith("⚠"));
            var group = min.GetVisualAncestors().OfType<Expander>().First();
            group.IsExpanded = false; Flush(window);
            var tab = window.GetVisualDescendants().OfType<ToggleButton>().Single(b => Equals(b.Content, "XML"));
            for (int i = 0; i < 2; i++)
            {
                tab.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(window);
                Assert.That(Status(window), Does.Contain("volume · min").And.Contain("min must be a number").And.Contain("Discard incomplete input"));
                Assert.That(tab.IsChecked, Is.False);
            }
            var show = Find<Button>(window, "showPreviewIssue");
            Assert.That(show.Content, Is.EqualTo("Show field"));
            show.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(window);
            Assert.That(group.IsExpanded, Is.True);
            Assert.That(min.IsFocused, Is.True);
            Assert.That(window.LayoutXml, Is.EqualTo(original));
            min.Text = "2"; Flush(window);
            Assert.That(Find<TextBlock>(window, "warning_volume").IsVisible, Is.False);
            Assert.That(Find<TextBlock>(window, "label_min").Text, Does.Not.StartWith("⚠"));
            Assert.That(Status(window), Does.Not.Contain("min must"));
            tab.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(window);
            Assert.That(tab.IsChecked, Is.True);
        }
        finally { Close(window); }
    }

    [AvaloniaTest]
    public void InvalidIdMarksTheExistingBlockAndDiscardRemovesBothWarnings()
    {
        var window = new EditorWindow(Original, "{}", true);
        window.Show(); Flush(window);
        try
        {
            Find<TextBox>(window, "field_id").Text = ""; Flush(window);
            Assert.That(Find<TextBlock>(window, "warning_title").IsVisible, Is.True);
            Assert.That(Find<TextBlock>(window, "label_id").Text, Does.StartWith("⚠"));
            Find<Button>(window, "discardProperties").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(window);
            Assert.That(Find<TextBlock>(window, "warning_title").IsVisible, Is.False);
            Assert.That(Find<TextBlock>(window, "label_id").Text, Does.Not.StartWith("⚠"));
        }
        finally { Close(window); }
    }

    [AvaloniaTest]
    public void InvalidStyleMinimumMarksOnlyThatConditionAndReopensIt()
    {
        var window = new EditorWindow("<slider id='volume' min='0'><style when='active' min='5'/></slider>", """{"active":true}""", true);
        window.Show(); Flush(window);
        try
        {
            var condition = window.GetVisualDescendants().OfType<Expander>().Single(e => e.Header?.ToString()?.StartsWith("Condition 1:") == true);
            condition.IsExpanded = true; Flush(window);
            var min = condition.GetVisualDescendants().OfType<TextBox>().Single(b => b.Name == "field_min");
            min.Text = "0a"; Flush(window);
            Assert.That(Status(window), Does.Contain("volume · Condition 1 · min"));
            Assert.That(Find<TextBlock>(window, "label_1_min").Text, Does.StartWith("⚠"));
            Assert.That(Find<TextBlock>(window, "label_min").Text, Does.Not.StartWith("⚠"));
            condition.IsExpanded = false; Flush(window);
            Find<Button>(window, "showPreviewIssue").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(window);
            Assert.That(condition.IsExpanded, Is.True);
            Assert.That(min.IsFocused, Is.True);
        }
        finally { Close(window); }
    }

    [AvaloniaTest]
    public void InvalidXmlHasAWarningEvenWithoutAnIdentifiableComponent()
    {
        var window = Open();
        try
        {
            Apply(window, "<text");
            Assert.That(Status(window), Does.StartWith("⚠").And.Contain("Discard XML changes"));
            var show = Find<Button>(window, "showPreviewIssue");
            Assert.That(show.IsVisible && show.IsEnabled, Is.True);
            show.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(window);
            Assert.That(Find<TextBox>(window, "xml").IsFocused, Is.True);
            Find<Button>(window, "discardXml").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(window);
            Assert.That(Status(window), Does.Not.Contain("⚠"));
            Assert.That(window.LayoutXml, Is.EqualTo(Original));
        }
        finally { Close(window); }
    }

    [AvaloniaTest]
    public void InvalidLiteralPropertyInputStaysPendingEvenWhenTextUsesABinding()
    {
        const string xml = "<text id='title'>{{title}}</text>";
        var window = new EditorWindow(xml, "{}", true);
        window.Show(); Flush(window);
        try
        {
            Find<TextBox>(window, "field_color").Text = "invalid"; Flush(window);
            Assert.That(window.LayoutXml, Is.EqualTo(xml));
            Assert.That(Find<Button>(window, "discardProperties").IsEnabled, Is.True);
            Assert.That(Status(window), Does.Contain("colors").And.Not.Contain("Layout accepted"));
            Find<TextBox>(window, "field_color").Text = "#12ab34"; Flush(window);
            Assert.That(window.LayoutXml, Does.Contain("#12ab34"));
            Assert.That(Find<Button>(window, "discardProperties").IsEnabled, Is.False);
        }
        finally { Close(window); }
    }

    [AvaloniaTest]
    public void UnrelatedTextBindingsDoNotAllowInvalidLiteralPropertiesToReplaceTheDraft()
    {
        var window = Open();
        try
        {
            foreach (var (name, value) in new[] { ("color", "invalid"), ("size", "invalid"), ("opacity", "2"), ("wrap", "invalid"),
                ("weight", "invalid"), ("maxLines", "1.5"), ("transitionMs", "-1") })
            {
                Apply(window, $"<text id='title' {name}='{value}'>{{{{title}}}}</text>");
                Assert.That(window.LayoutXml, Is.EqualTo(Original), name);
                Assert.That(Find<Button>(window, "discardXml").IsEnabled, Is.True, name);
                Assert.That(Status(window), Does.Not.Contain("Layout accepted"), name);
                Assert.That(Find<Button>(window, "undo").IsEnabled, Is.False, "Rejected input must not add history.");
            }
        }
        finally { Close(window); }
    }

    [AvaloniaTest]
    public void AnActiveStyleDeterminesWhetherTheFailingColorCanChangeWithLiveData()
    {
        var window = Open("""{"active":true,"tint":"invalid"}""");
        try
        {
            Apply(window, "<text id='title' color='{{tint}}'><style when='active' color='invalid'/>Title</text>");
            Assert.That(window.LayoutXml, Is.EqualTo(Original));
            Assert.That(Status(window), Does.Not.Contain("Layout accepted"));

            const string dynamicStyle = "<text id='title' color='invalid'><style when='active' color='{{tint}}'/>Title</text>";
            Apply(window, dynamicStyle);
            Assert.That(window.LayoutXml, Is.EqualTo(dynamicStyle));
            Assert.That(Status(window), Does.Contain("cannot preview"));
            var discard = Find<Button>(window, "discardXml");
            Assert.That(discard.IsEnabled, Is.True);
            Assert.That(discard.Content, Is.EqualTo("Undo last edit"));
            discard.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(window);
            Assert.That(window.LayoutXml, Is.EqualTo(Original));
        }
        finally { Close(window); }
    }

    [AvaloniaTest]
    public void MissingAndValidDynamicValuesRemainEditable()
    {
        const string xml = "<text id='title' color='{{tint}}' size='{{size}}'>{{title}}</text>";
        var missing = Open();
        try
        {
            Apply(missing, xml);
            Assert.That(missing.LayoutXml, Is.EqualTo(xml));
            Assert.That(Status(missing), Does.Contain("cannot preview"));
        }
        finally { Close(missing); }

        var available = Open("""{"tint":"#12ab34","size":"20%","title":"Ready"}""");
        try
        {
            Apply(available, xml);
            Assert.That(available.LayoutXml, Is.EqualTo(xml));
            Assert.That(Status(available), Does.Not.Contain("cannot preview"));
        }
        finally { Close(available); }
    }

    [AvaloniaTest]
    public void DynamicRangeAndStepRelationshipsCanBeSavedUntilLiveDataIsAvailable()
    {
        var window = Open("""{"limit":1}""");
        try
        {
            foreach (var xml in new[] { "<slider id='level' min='5' max='{{limit}}'/>", "<slider id='level' max='{{limit}}' step='2'/>" })
            {
                Apply(window, xml);
                Assert.That(window.LayoutXml, Is.EqualTo(xml));
                Assert.That(Status(window), Does.Contain("cannot preview"));
            }
        }
        finally { Close(window); }
    }

    [AvaloniaTest]
    public void RepeatStylesUseTheFailingItemsScopeWhenClassifyingLiteralAndDynamicErrors()
    {
        var window = Open("""{"rows":[{"active":false},{"active":true,"tint":"invalid"}]}""");
        try
        {
            const string start = "<repeat id='rows' items='rows'><text id='title' color='#fff'><style when='item.active' color='";
            const string end = "'/>{{item.title}}</text></repeat>";
            Apply(window, start + "invalid" + end);
            Assert.That(window.LayoutXml, Is.EqualTo(Original));
            var dynamicXml = start + "{{item.tint}}" + end;
            Apply(window, dynamicXml);
            Assert.That(window.LayoutXml, Is.EqualTo(dynamicXml));
            Assert.That(Status(window), Does.Contain("cannot preview"));
        }
        finally { Close(window); }
    }

    [AvaloniaTest]
    public void ComponentScalarErrorsAreRejectedEvenWhenAnotherPropertyUsesABinding()
    {
        var window = Open();
        try
        {
            foreach (var xml in new[] {
                "<trackpad id='node' color='invalid' opacity='{{alpha}}'/>",
                "<trackpad id='node' background='invalid' opacity='{{alpha}}'/>",
                "<chart id='node' key='history' points='2.5' opacity='{{alpha}}'/>",
                "<progress-bar id='node' positionMs='1.5' opacity='{{alpha}}'/>",
                "<progress-bar id='node' durationMs='1.5' opacity='{{alpha}}'/>",
                "<progress-bar id='node' anchor='invalid' opacity='{{alpha}}'/>" })
            {
                Apply(window, xml);
                Assert.That(window.LayoutXml, Is.EqualTo(Original), xml);
                Assert.That(Find<Button>(window, "discardXml").IsEnabled, Is.True, xml);
            }
        }
        finally { Close(window); }
    }

    [AvaloniaTest]
    public void ComponentScalarBindingsAcceptCurrentDataErrorsAndRenderWithValidData()
    {
        var window = Open("""{"invalid":"invalid","fraction":2.5,"color":"#12ab34","whole":10,"stamp":"2026-09-13T12:00:00+09:00"}""");
        try
        {
            foreach (var (invalid, valid) in new[] {
                ("<trackpad id='node' color='{{invalid}}' background='{{color}}'/>", "<trackpad id='node' color='{{color}}' background='{{color}}'/>"),
                ("<trackpad id='node' background='{{invalid}}'/>", "<trackpad id='node' background='{{color}}'/>"),
                ("<chart id='node' key='history' points='{{fraction}}'/>", "<chart id='node' key='history' points='{{whole}}'/>"),
                ("<progress-bar id='node' positionMs='{{fraction}}'/>", "<progress-bar id='node' positionMs='{{whole}}'/>"),
                ("<progress-bar id='node' durationMs='{{fraction}}'/>", "<progress-bar id='node' durationMs='{{whole}}'/>"),
                ("<progress-bar id='node' anchor='{{invalid}}'/>", "<progress-bar id='node' anchor='{{stamp}}'/>") })
            {
                Apply(window, invalid);
                Assert.That(window.LayoutXml, Is.EqualTo(invalid));
                Assert.That(Status(window), Does.Contain("cannot preview"));
                Apply(window, valid);
                Assert.That(window.LayoutXml, Is.EqualTo(valid));
                Assert.That(Status(window), Does.Not.Contain("cannot preview"));
            }
        }
        finally { Close(window); }
    }

    [AvaloniaTest]
    public void ResponsiveValidationUsesTheVariantBindingsAndRejectsUnrelatedLiteralErrors()
    {
        var window = Open("""{"active":true,"invalid":"invalid","width":2}""");
        try
        {
            const string prefix = "<responsive id='layout'><variant id='compact'/><variant id='wide' ";
            const string suffix = "/></responsive>";
            var dynamic = prefix + "minWidth='{{invalid}}'" + suffix;
            Apply(window, dynamic);
            Assert.That(window.LayoutXml, Is.EqualTo(dynamic));
            Assert.That(Status(window), Does.Contain("cannot preview").And.Contain("ID 'wide'"));

            Apply(window, prefix + "minWidth='invalid' maxWidth='{{width}}'" + suffix);
            Assert.That(window.LayoutXml, Is.EqualTo(dynamic), "An unrelated maximum binding cannot repair a literal invalid minimum.");

            var dynamicStyle = prefix + "minWidth='invalid'><style when='active' minWidth='{{invalid}}'/></variant></responsive>";
            Apply(window, dynamicStyle);
            Assert.That(window.LayoutXml, Is.EqualTo(dynamicStyle));
            Assert.That(Status(window), Does.Contain("cannot preview"));
            Apply(window, prefix + "minWidth='{{width}}'><style when='active' minWidth='invalid'/></variant></responsive>");
            Assert.That(window.LayoutXml, Is.EqualTo(dynamicStyle));

            var range = prefix + "minWidth='5' maxWidth='{{width}}'" + suffix;
            Apply(window, range);
            Assert.That(window.LayoutXml, Is.EqualTo(range), "The minimum/maximum relationship can change with live data.");
            Assert.That(Status(window), Does.Contain("cannot preview"));

            var valid = prefix + "minWidth='{{width}}'" + suffix;
            Apply(window, valid);
            Assert.That(window.LayoutXml, Is.EqualTo(valid));
            Assert.That(Status(window), Does.Not.Contain("cannot preview"));
        }
        finally { Close(window); }
    }

    [AvaloniaTest]
    public void WarningLinksToTheActiveStyleAndRevertsOnlyItsAttribute()
    {
        const string original = "<text id='title' color='#fff'>\r\n  <style when='active' color='#abc'/>Title</text>";
        var window = new EditorWindow(original, """{"active":true}""", true);
        window.Show(); Flush(window);
        try
        {
            var edited = original.Replace("#abc", "{{missing}}").Replace("Title</text>", "Edited</text>");
            Apply(window, edited);
            Assert.That(Status(window), Does.Contain("title · Condition 1 · color"));
            Find<Button>(window, "showPreviewIssue").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(window);
            var xml = Find<TextBox>(window, "xml");
            Assert.That(xml.SelectedText, Is.EqualTo("color"));
            Assert.That(xml.SelectionStart, Is.EqualTo(xml.Text!.LastIndexOf("color", StringComparison.Ordinal)));
            var discard = Find<Button>(window, "discardXml");
            Assert.That(discard.Content, Is.EqualTo("Revert color"));
            discard.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(window);
            Assert.That(window.LayoutXml, Does.Contain("#abc").And.Contain("Edited</text>"));
            Assert.That(discard.IsEnabled, Is.False);
            Assert.That(Status(window), Does.Not.Contain("cannot preview"));
        }
        finally { Close(window); }
    }

    [AvaloniaTest]
    public void RevertingAPropertyWarningKeepsSubsequentPropertyEdits()
    {
        var window = new EditorWindow(Original, "{}", true);
        window.Show(); Flush(window);
        try
        {
            Find<TextBox>(window, "field_color").Text = "{{missing}}"; Flush(window);
            Find<TextBox>(window, "field_opacity").Text = "0.5"; Flush(window);
            Assert.That(Find<TextBlock>(window, "label_color").Text, Does.StartWith("⚠"));
            var discard = Find<Button>(window, "discardProperties");
            Assert.That(discard.IsEnabled, Is.True);
            Assert.That(Find<TextBlock>(window, "warning_title").IsVisible, Is.True);
            Assert.That(discard.Content, Is.EqualTo("Revert color"));
            discard.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(window);
            Assert.That(window.LayoutXml, Does.Contain("0.5").And.Not.Contain("{{missing}}"));
            Assert.That(Find<TextBlock>(window, "warning_title").IsVisible, Is.False);
            Assert.That(Find<Button>(window, "discardProperties").IsEnabled, Is.False);
        }
        finally { Close(window); }
    }

    [AvaloniaTest]
    public void OpeningAnExistingWarningProvidesNavigationWithoutInventingHistory()
    {
        var window = new EditorWindow("<text id='title' color='{{missing}}'/>", "{}", true);
        window.Show(); Flush(window);
        try
        {
            Assert.That(Status(window), Does.Contain("title · color"));
            Assert.That(Find<Button>(window, "discardProperties").IsEnabled, Is.False);
            Find<Button>(window, "showPreviewIssue").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(window);
            Assert.That(Find<TextBox>(window, "xml").SelectedText, Is.EqualTo("color"));
        }
        finally { Close(window); }
    }

    private static EditorWindow Open(string data = "{}")
    {
        var window = new EditorWindow(Original, data, true);
        window.Show(); Flush(window);
        window.GetVisualDescendants().OfType<ToggleButton>().Single(b => b.Content?.ToString() == "XML")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Flush(window);
        return window;
    }
    private static void Apply(EditorWindow window, string xml)
    {
        Find<TextBox>(window, "xml").Text = xml;
        window.GetVisualDescendants().OfType<Button>().Single(b => b.Content?.ToString() == "Apply XML")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Flush(window);
    }
    private static T Find<T>(EditorWindow window, string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    private static string? Status(EditorWindow window) => Find<TextBlock>(window, "status").Text;
    private static void Flush(EditorWindow window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Close(EditorWindow window) { window.HostClosed = true; window.Close(); }
}
