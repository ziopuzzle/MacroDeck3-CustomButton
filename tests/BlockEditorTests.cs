using System.Text.Json;
using System.Xml.Linq;
using MacroDeck.Ui.Model.Events;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class BlockEditorTests
{
    [Test] public void DragBetweenParentsAndAroundSiblingsPreservesSubtreesAndStyles()
    {
        var history = new LayoutEditHistory("<stack id='root'><stack id='a'><text id='t'>Hi<style when='true' color='#fff'/></text></stack><stack id='b'/></stack>");
        history.Edit(d => { d.Place("t", "b", "inside"); return "t"; });
        Assert.That(new LayoutDocument(history.Xml).Find("t").Parent!.Attribute("id")!.Value, Is.EqualTo("b"));
        history.Edit(d => { d.Place("b", "a", "before"); return "b"; });
        Assert.That(XElement.Parse(history.Xml).Elements().First().Attribute("id")!.Value, Is.EqualTo("b"));
        history.Edit(d => { d.Place("b", "a", "after"); return "b"; });
        Assert.That(XElement.Parse(history.Xml).Elements().Last().Attribute("id")!.Value, Is.EqualTo("b"));
        Assert.That(XElement.Parse(history.Xml).Descendants("style").Single().Attribute("color")!.Value, Is.EqualTo("#fff"));
        history.Undo(); Assert.That(XElement.Parse(history.Xml).Elements().First().Attribute("id")!.Value, Is.EqualTo("b"));
        history.Redo(); Assert.That(XElement.Parse(history.Xml).Elements().Last().Attribute("id")!.Value, Is.EqualTo("b"));
    }
    [TestCase("root", "a", "inside")]
    [TestCase("a", "t", "after")]
    [TestCase("a", "a", "inside")]
    [TestCase("t", "root", "before")]
    [TestCase("a", "t", "inside")]
    public void InvalidDragDoesNotChangeDocumentOrHistory(string source, string target, string position)
    {
        var original = "<stack id='root'><stack id='a'><text id='t'>Hi</text></stack></stack>";
        var history = new LayoutEditHistory(original);
        Assert.Throws<FormatException>(() => history.Edit(d => { d.Place(source, target, position); return source; }));
        Assert.That(history.Xml, Is.EqualTo(original)); Assert.That(history.CanUndo, Is.False);
    }
    [Test] public void PaletteInsertionAndDuplicateCreateUniqueIdsAndRespectDropPosition()
    {
        var history = new LayoutEditHistory("<stack id='root'><text id='t'>Hi</text></stack>");
        var added = history.Edit(d => { var id = d.Add("t", "stack"); d.Place(id, "t", "before"); return id; });
        history.Edit(d => d.Add(added, "text")); history.Edit(d => d.Duplicate(added));
        var nodes = XElement.Parse(history.Xml).DescendantsAndSelf().ToArray();
        Assert.That(nodes.Select(n => (string)n.Attribute("id")!).Distinct().Count(), Is.EqualTo(nodes.Length));
        Assert.That(nodes[1].Name.LocalName, Is.EqualTo("stack"));
    }
    [Test] public void FailedPropertyAndOversizedEditsLeaveXmlAndUndoIntact()
    {
        var original = "<stack id='root'>" + string.Concat(Enumerable.Range(0, LayoutLimits.XmlElements - 1).Select(i => $"<text id='t{i}'>A</text>")) + "</stack>";
        var history = new LayoutEditHistory(original);
        Assert.Throws<FormatException>(() => history.Edit(d => d.Add("root", "text")));
        Assert.Throws<FormatException>(() => history.Edit(d => { d.SetAttribute("t0", "visibleWhen", "value >="); return "t0"; }));
        Assert.That(history.Xml, Is.EqualTo(original)); Assert.That(history.CanUndo, Is.False);
    }
    [Test] public void PropertiesKeepTextEscapingConditionsAndUneditedAttributes()
    {
        var history = new LayoutEditHistory("<text id='t' color='#fff'>Hi<style when='value >= 80' color='#f00'/></text>");
        history.Edit(d => { d.SetText("t", "{{title}} & <literal>"); d.SetAttribute("t", "size", "25%"); d.SetStyle("t", 0, "when", "value >= 50"); return "t"; });
        var node = XElement.Parse(history.Xml);
        Assert.That(node.Nodes().OfType<XText>().Single().Value, Is.EqualTo("{{title}} & <literal>"));
        Assert.That((string?)node.Attribute("color"), Is.EqualTo("#fff"));
        Assert.That((string?)node.Element("style")!.Attribute("color"), Is.EqualTo("#f00"));
        var updated = history.Xml; history.Undo(); history.Redo(); Assert.That(history.Xml, Is.EqualTo(updated));
        history.Undo(); history.Set("<text id='other'>XML</text>"); Assert.That(history.CanRedo, Is.False);
    }
    [Test] public async Task ConfigurationKeepsXmlVisibleAndGroupsEditorAndTemplatesWithHeading()
    {
        await using var session = new ConfigurationSession(ButtonTests.Surface("config"), TestLayouts.Sample);
        var nodes = ButtonTests.Nodes(session.BuildTree().Root).ToArray();
        var tabs = nodes.Single(n => n.Type == "tabs");
        var sidebar = nodes.Single(n => n.Id.EndsWith(".properties"));
        Assert.That(ButtonTests.Nodes(sidebar).Any(n => n.Id == "initialValues"), Is.True);
        Assert.That(ButtonTests.Nodes(tabs).Any(n => n.Id == "initialValues"), Is.False);
        Assert.That(nodes.Any(n => n.Id.EndsWith("editorLanguage")), Is.False);
        Assert.That(tabs.Children.Select(n => LocalizationTests.Resolve(n.Properties["label"])), Is.EqualTo(new[] { "Actions", "Drawing" }));
        var toolbar = nodes.Single(n => n.Id.EndsWith(".templateToolbar"));
        Assert.That(toolbar.Children.Select(n => n.Id.Split('.').Last()), Is.EqualTo(new[] { "layoutHeading", "openNativeEditor", "templatePicker" }));
        var mode = nodes.Single(n => n.Id == "designPreset");
        Assert.That(mode.Properties["visibleWhen"].GetProperty("values").GetArrayLength(), Is.Zero);
        Assert.That(toolbar.Properties["direction"].GetString(), Is.EqualTo("vertical"));
        Assert.That(nodes.Any(n => n.Id.StartsWith("blockSelect-")), Is.False);
        var input = nodes.Single(n => n.Id == "layout" || n.Id.EndsWith(".layout"));
        Assert.That(input.Properties["hideLabel"].GetBoolean(), Is.True);
        session.Dispatch(new UiEvent { NodeId = input.Id, Name = "change", Data = JsonSerializer.SerializeToElement("<text id='new'>XML</text>") });
        Assert.That(ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id == input.Id).Properties["value"].GetString(), Is.EqualTo("<text id='new'>XML</text>"));
    }
    [Test] public async Task InvalidXmlLaunchReportsInlineAndPreservesDraftWithoutStartingAnApp()
    {
        await using var session = new ConfigurationSession(ButtonTests.Surface("config"), TestLayouts.Sample with { Layout = "<invalid>" });
        var button = ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id.EndsWith(".openNativeEditor"));
        session.Dispatch(new UiEvent { NodeId = button.Id, Name = "activate" });
        var nodes = ButtonTests.Nodes(session.BuildTree().Root).ToArray();
        Assert.That(LocalizationTests.Resolve(nodes.Single(n => n.Id.EndsWith(".nativeStatusText")).Properties["text"]), Does.StartWith("Cannot open editor:"));
        Assert.That(nodes.Single(n => n.Id == "layout" || n.Id.EndsWith(".layout")).Properties["value"].GetString(), Is.EqualTo("<invalid>"));
        Assert.That(session.DrainPatches(), Is.Not.Empty);
    }
}



