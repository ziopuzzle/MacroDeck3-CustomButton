using System.Xml.Linq;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class LayoutFormattingTests
{
    [TestCase("image")][TestCase("icon")][TestCase("bar")][TestCase("line")]
    public void StylesInsideLeafComponentsAreIndentedAndRemainInOrder(string type)
    {
        var xml = $"<stack id='root'><{type} id='art'>\n    <style when='state == 1' color='#ff0000'/>\n<style when='state == 2' color='#00ff00'/><style when='state == 3' color='#0000ff'/></{type}></stack>";
        var formatted = new LayoutDocument(xml).Serialize();
        Assert.That(formatted, Is.EqualTo($"<stack id=\"root\">\n  <{type} id=\"art\">\n    <style when=\"state == 1\" color=\"#ff0000\" />\n    <style when=\"state == 2\" color=\"#00ff00\" />\n    <style when=\"state == 3\" color=\"#0000ff\" />\n  </{type}>\n</stack>"));
        Assert.That(new LayoutDocument(formatted).Serialize(), Is.EqualTo(formatted));
    }

    [Test] public void FormattingPreservesSvgCdataAlongsideStyles()
    {
        const string content = "\n<svg width='100' height='100'>\n  <text x='10' y='20'> A &amp; B </text>\n</svg>\n";
        var xml = "<svg id='art'><![CDATA[" + content + "]]><style when='state == 1' color='#ff0000'/></svg>";
        var formatted = new LayoutDocument(xml).Serialize();
        var saved = XElement.Parse(formatted, LoadOptions.PreserveWhitespace);
        Assert.That(saved.Nodes().OfType<XCData>().Single().Value, Is.EqualTo(content));
        Assert.That(new LayoutDocument(formatted).Serialize(), Is.EqualTo(formatted));
    }
    [Test] public void GuiEditsRemoveOrphanIndentationAndPutEachComponentOnItsOwnLine()
    {
        var history = new LayoutEditHistory("<stack id='root'>\n  \n  <layer id='layer'><chart id='chart' key='cpu_value'/><stack id='row'><text id='value'>{{cpu_value}}%</text><bar id='bar' value='{{value}}'/></stack></layer></stack>");
        history.Edit(d => { d.Place("bar", "value", "before"); return "bar"; });
        Assert.That(history.Xml, Is.EqualTo("""
            <stack id="root">
              <layer id="layer">
                <chart id="chart" key="cpu_value" />
                <stack id="row">
                  <bar id="bar" value="{{value}}" />
                  <text id="value">{{cpu_value}}%</text>
                </stack>
              </layer>
            </stack>
            """.Replace("\r\n", "\n")));
        Assert.That(new LayoutDocument(history.Xml).Serialize(), Is.EqualTo(history.Xml));
        var saved = history.Xml; history.Undo(); history.Redo(); Assert.That(history.Xml, Is.EqualTo(saved));
    }

    [TestCase("  \n  ")]
    [TestCase(" A &amp; B \n {{value}} &#xD; C ")]
    public void FormattingPreservesVisibleWhitespaceEntitiesAndMixedTextStyleContent(string content)
    {
        var xml = $"<stack id='root'> \n <!--keep--><text id='label'>{content}<style when='value >= 80' color='#f00'/> tail </text></stack>";
        var before = XElement.Parse(xml, LoadOptions.PreserveWhitespace).Element("text")!;
        var formatted = new LayoutDocument(xml).Serialize();
        var after = XElement.Parse(formatted, LoadOptions.PreserveWhitespace).Element("text")!;
        Assert.That(XNode.DeepEquals(before, after), Is.True);
        Assert.That(formatted, Does.Contain("<!--keep-->"));
        Assert.That(new LayoutDocument(formatted).Serialize(), Is.EqualTo(formatted));
    }

    [Test] public void WhitespaceOnlyLabelIsNotLost()
    {
        var formatted = new LayoutDocument("<stack id='root'><text id='blank'>   </text></stack>").Serialize();
        Assert.That(XElement.Parse(formatted, LoadOptions.PreserveWhitespace).Element("text")!.Value, Is.EqualTo("   "));
    }

    [Test] public void FormattingThatExceedsSizeLimitLeavesTheDraftAndHistoryUntouched()
    {
        var xml = "<stack id='root'><text id='t'>" + new string('a', LayoutLimits.XmlCharacters - 60) + "</text></stack>";
        var history = new LayoutEditHistory(xml);
        Assert.Throws<FormatException>(() => history.Edit(d => d.Add("root", "bar")));
        Assert.That(history.Xml, Is.EqualTo(xml)); Assert.That(history.CanUndo, Is.False);
    }
}

