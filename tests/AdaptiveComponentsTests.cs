using MacroDeck.Ui.Model.Nodes;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class AdaptiveComponentsTests
{
    private static UiNode Find(ButtonSession session, string id) => ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id.EndsWith("." + id));

    [Test]
    public async Task RangeUsesOneScaleAndMarkerIsOptional()
    {
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with {
            Layout = "<layer id='s'><bar id='range' min='-100' max='100' start='-50' value='50' marker='200'/><bar id='plain' value='20'/></layer>" }, null);
        var range = Find(session, "range");
        Assert.That(range.Properties["start"].GetDouble(), Is.EqualTo(.25));
        Assert.That(range.Properties["end"].GetDouble(), Is.EqualTo(.75));
        Assert.That(range.Properties["marker"].GetDouble(), Is.EqualTo(1));
        Assert.That(Find(session, "plain").Properties.ContainsKey("marker"), Is.False);
    }

    [Test]
    public async Task DynamicClockKeepsReferenceWithoutServerTickPatches()
    {
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with {
            Layout = "<dynamic-text id='time' zone='Asia/Tokyo' format='time-24h' seconds='true'/>" }, null);
        var time = Find(session, "time");
        Assert.That(time.Type, Is.EqualTo("macrodeck.dynamic-text"));
        Assert.That(time.Properties["value"].GetProperty("$time").GetProperty("zone").GetString(), Is.EqualTo("Asia/Tokyo"));
        Assert.That(time.Properties["format"].GetString(), Is.EqualTo("time-24h"));
        session.Refresh(); Assert.That(session.DrainPatches(), Is.Empty);
    }

    [Test]
    public async Task ResponsiveKeepsAllBranchesAndTheirConditionsInOrder()
    {
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with {
            Layout = "<responsive id='r'><variant id='compact'><text id='a'>A</text></variant><variant id='wide' minAspect='1.5'><text id='b'>B</text></variant></responsive>" }, null);
        var responsive = Find(session, "r");
        Assert.That(responsive.Type, Is.EqualTo("ui.responsive"));
        Assert.That(responsive.Properties["variants"][0].GetProperty("minAspect").GetDouble(), Is.EqualTo(1.5));
        Assert.That(Find(session, "a"), Is.Not.Null);
        Assert.That(Find(session, "b"), Is.Not.Null);
    }

    [Test]
    public async Task ModifierAppliesOpacityOnceAndPreservesChild()
    {
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with {
            Layout = "<modifier id='frame' clip='circle' width='50%' opacity='0.5'><text id='label'>A</text></modifier>" }, null);
        var error = ButtonTests.Nodes(session.BuildTree().Root).FirstOrDefault(n => n.Id.EndsWith(".message"));
        Assert.That(error, Is.Null, error == null ? "" : LocalizationTests.Resolve(error.Properties["text"]));
        var modifier = Find(session, "frame");
        Assert.That(modifier.Properties["opacity"].GetDouble(), Is.EqualTo(.5));
        Assert.That(modifier.Properties["clip"].GetString(), Is.EqualTo("circle"));
        Assert.That(modifier.Properties["frame"].GetProperty("width").GetProperty("basis").GetDouble(), Is.EqualTo(.5));
        Assert.That(Find(session, "label").Type, Is.EqualTo("ui.text"));
    }

    [TestCase("<dynamic-text id='x' format='bad'/>", "format")]
    [TestCase("<modifier id='x' minWidth='80%' maxWidth='20%'/>", "maximum")]
    [TestCase("<responsive id='r'><variant id='d'/><variant id='w' minAspect='2' maxAspect='1'/></responsive>", "maximum")]
    public async Task InvalidPropertiesShowErrors(string xml, string error)
    {
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with { Layout = xml }, null);
        Assert.That(LocalizationTests.Resolve(Find(session, "message").Properties["text"]), Does.Contain(error));
    }
}
