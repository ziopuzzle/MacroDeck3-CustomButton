using NUnit.Framework;
using System.Xml;

namespace Ziopuzzle.CustomButton.Tests;

public class EditorDiagnosticsTests
{
    [Test]
    public void DuplicateIdReportsBothLocations()
    {
        var error = Assert.Throws<FormatException>(() => new LayoutRenderer("<stack id='root'>\n  <text id='same'>A</text>\n  <bar id='same'/>\n</stack>"))!;
        Assert.That(error.Message, Does.Contain("Duplicate ID 'same'").And.Contain("<bar>").And.Contain("line 2").And.Contain("line 3"));
        Assert.That(error.Data["LayoutLine"], Is.EqualTo(3));
    }
    [TestCase("<unknown id='bad'/>", "Unsupported component")]
    [TestCase("<text id='bad' typo='1'/>", "Unsupported attribute")]
    [TestCase("<text id='bad' visibleWhen='x >'/>", "bad")]
    public void InvalidComponentReportsLocation(string child, string message)
    {
        var error = Assert.Throws<FormatException>(() => new LayoutRenderer("<stack id='root'>\n" + child + "\n</stack>"))!;
        Assert.That(error.Message, Does.Contain(message).And.Contain("ID 'bad'"));
        Assert.That(error.Data["LayoutLine"], Is.EqualTo(2));
    }
    [Test]
    public void RenderErrorNamesTheChildRatherThanItsParent()
    {
        var error = Assert.Throws<FormatException>(() => new LayoutRenderer("<stack id='root'>\n<text id='title' color='nonsense'>A</text></stack>").Render(DataHub.ParseValues("{}")))!;
        Assert.That(error.Message, Does.Contain("ID 'title'").And.Not.Contain("ID 'root'"));
        Assert.That(error.Data["LayoutLine"], Is.EqualTo(2));
    }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    [Test]
    public void HistoryGroupsOnlyContinuousChangesToTheSameField()
    {
        var clock = new Clock();
        string Xml(string value) => $"<text id='t'>{value}</text>";
        var history = new LayoutEditHistory(Xml("a"), clock);
        history.SetGrouped(Xml("b"), "text"); history.SetGrouped(Xml("c"), "text");
        history.SetGrouped(Xml("d"), "color");
        history.Undo(); Assert.That(history.Xml, Is.EqualTo(Xml("c")));
        history.Undo(); Assert.That(history.Xml, Is.EqualTo(Xml("a")));
        history.Redo(); Assert.That(history.Xml, Is.EqualTo(Xml("c")));
        clock.Now += TimeSpan.FromSeconds(1);
        history.SetGrouped(Xml("e"), "text");
        Assert.That(history.CanRedo, Is.False);
        clock.Now += TimeSpan.FromSeconds(1);
        history.SetGrouped(Xml("f"), "text");
        history.Undo(); Assert.That(history.Xml, Is.EqualTo(Xml("e")));
        Assert.Throws<FormatException>(() => history.SetGrouped("<text/>", "text"));
        Assert.That(history.Xml, Is.EqualTo(Xml("e")));
    }
}
