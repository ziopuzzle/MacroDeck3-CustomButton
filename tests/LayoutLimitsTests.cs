using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class LayoutLimitsTests
{
    private static string Nested(int depth) => string.Concat(Enumerable.Range(0, depth).Select(i => $"<stack id='n{i}'>")) + "<text id='label'>OK</text>" + string.Concat(Enumerable.Repeat("</stack>", depth));
    [Test] public async Task MoreThan64ComponentsAndMoreThanEightLevelsRender()
    {
        var wide = "<stack id='root'>" + string.Concat(Enumerable.Range(0, 80).Select(i => $"<text id='t{i}'>OK</text>")) + "</stack>";
        foreach (var xml in new[] { wide, Nested(9) })
        {
            await using var session = new ButtonSession(ButtonTests.Surface(), new("demo", xml, "{}"), null);
            Assert.That(ButtonTests.Nodes(session.BuildTree().Root).Any(n => n.Id.EndsWith(".message")), Is.False);
            Assert.DoesNotThrow(() => MacroDeck.Ui.Model.Serialization.UiCanonicalJson.Serialize(session.BuildTree()));
        }
    }
    [Test] public void LargerXmlCanBeEditedAndSaved()
    {
        var xml = "<stack id='root'>" + new string(' ', 17000) + "</stack>";
        Assert.DoesNotThrow(() => new LayoutDocument(xml).Serialize());
        Assert.Throws<FormatException>(() => new LayoutRenderer(new string(' ', LayoutLimits.XmlCharacters + 1)));
    }
    [Test] public async Task ExcessiveRenderedDepthBecomesAnErrorInsteadOfBreakingTheSession()
    {
        await using var session = new ButtonSession(ButtonTests.Surface(), new("demo", Nested(20), "{}"), null);
        Assert.That(ButtonTests.Nodes(session.BuildTree().Root).Any(n => n.Id.EndsWith(".message")), Is.True);
    }
    [Test] public void XmlSafetyLimitsStillApply()
    {
        Assert.Throws<FormatException>(() => new LayoutRenderer(Nested(32)));
        var xml = "<stack id='root'>" + string.Concat(Enumerable.Range(0, 512).Select(i => $"<text id='t{i}'/>")) + "</stack>";
        Assert.Throws<FormatException>(() => new LayoutRenderer(xml));
    }
}
