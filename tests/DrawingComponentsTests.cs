using MacroDeck.Ui.Model.Nodes;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class DrawingComponentsTests
{
    private static UiNode Find(ButtonSession session, string id) => ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id.EndsWith("." + id));

    [Test]
    public async Task GaugeNormalizesAndClampsLiveDataAndAppliesConditionalColor()
    {
        var hub = new DataHub();
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with {
            Layout = "<gauge id='g' value='{{value}}' min='-50' max='50'><style when='value > 40' color='#f00'/></gauge>",
            InitialValues = "{\"value\":0}" }, hub);
        Assert.That(Find(session, "g").Properties["level"].GetDouble(), Is.EqualTo(.5));
        hub.Update("demo", DataHub.ParseValues("{\"value\":200}")); session.Refresh();
        Assert.That(Find(session, "g").Properties["level"].GetDouble(), Is.EqualTo(1));
        Assert.That(Find(session, "g").Properties["levelColor"].GetString(), Is.EqualTo("#ff0000"));
    }

    [Test]
    public async Task TransformPreservesChildAndUpdatesRotationFromExpression()
    {
        var hub = new DataHub();
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with {
            Layout = "<transform id='group' rotation='{{=value * 2}}' originY='1.2' offsetX='-0.25'><icon id='symbol' name='arrow-up' role='muted'/></transform>",
            InitialValues = "{\"value\":15}" }, hub);
        Assert.That(Find(session, "group").Properties["rotation"].GetDouble(), Is.EqualTo(30));
        Assert.That(Find(session, "group").Properties["originY"].GetDouble(), Is.EqualTo(1.2));
        Assert.That(Find(session, "symbol").Properties["icon"].GetString(), Is.EqualTo("arrow-up"));
        hub.Update("demo", DataHub.ParseValues("{\"value\":45}")); session.Refresh();
        Assert.That(Find(session, "group").Properties["rotation"].GetDouble(), Is.EqualTo(90));
    }

    [TestCase("<gauge id='g' min='50' max='50'/>", "max")]
    [TestCase("<icon id='i' name='not-an-icon'/>", "name")]
    [TestCase("<transform id='t' zoom='-1'/>", "zoom")]
    public async Task InvalidInputsShowError(string xml, string expected)
    {
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with { Layout = xml }, null);
        Assert.That(LocalizationTests.Resolve(Find(session, "message").Properties["text"]), Does.Contain(expected));
    }

    [Test]
    public void EditorCanInsertAndMoveIntoTransform()
    {
        var doc = new LayoutDocument("<stack id='root'><text id='label'>Text</text></stack>");
        var group = doc.Add("root", "transform");
        doc.Add(group, "icon"); doc.Add(group, "gauge");
        doc.Place("label", group, "inside");
        var restored = new LayoutDocument(doc.Serialize());
        Assert.That(restored.Find("label").Parent!.Attribute("id")!.Value, Is.EqualTo(group));
        Assert.That(restored.Find(group).Elements().Count(), Is.EqualTo(3));
    }
}
