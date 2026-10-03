using System.Xml.Linq;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class LayoutFormattingTests
{
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

