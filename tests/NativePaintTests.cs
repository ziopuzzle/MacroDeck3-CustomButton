using MacroDeck.Ui.Runtime;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class NativePaintTests
{
    [Test]
    public async Task TextPaintsHaveIndependentAlphaAndConditionalRemoval()
    {
        var hub = new DataHub();
        await using var session = new ButtonSession(ButtonTests.Surface(), new ButtonSettings("demo", """
            <text id="title" color="#ffffff40" strokeColor="{{outline}}" strokeWidth="1%" shadow="false" maxLines="2">
              Title<style when="reset == 1" strokeColor="" shadow="" />
            </text>
            """, """{"outline":"#000000c0","reset":0}"""), hub);
        var text = ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Type == "ui.text");
        Assert.That(text.Properties["color"].GetString(), Is.EqualTo("#ffffff40"));
        Assert.That(text.Properties["strokeColor"].GetString(), Is.EqualTo("#000000c0"));
        Assert.That(text.Properties["strokeWidth"].GetProperty("basis").GetDouble(), Is.EqualTo(.01));
        Assert.That(text.Properties["shadow"].GetBoolean(), Is.False);
        hub.Update("demo", DataHub.ParseValues("""{"reset":1}""")); session.Refresh();
        text = ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Type == "ui.text");
        Assert.That(text.Properties.ContainsKey("strokeColor"), Is.False);
        Assert.That(text.Properties["shadow"].GetBoolean(), Is.False);
    }

    [Test]
    public async Task GaugeTrackFollowsDataWithoutFadingLevelAndCanRestoreTheme()
    {
        var hub = new DataHub();
        await using var session = new ButtonSession(ButtonTests.Surface(), new ButtonSettings("demo", """
            <gauge id="g" value="50" color="#00ff0040" trackColor="{{track}}">
              <style when="reset == 1" trackColor="" />
            </gauge>
            """, """{"track":"#ffffff80","reset":0}"""), hub);
        var gauge = ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Type == "ui.gauge");
        Assert.That(gauge.Properties["levelColor"].GetString(), Is.EqualTo("#00ff0040"));
        Assert.That(gauge.Properties["trackColor"].GetString(), Is.EqualTo("#ffffff80"));
        hub.Update("demo", DataHub.ParseValues("""{"track":"#ff0000"}""")); session.Refresh();
        gauge = ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Type == "ui.gauge");
        Assert.That(gauge.Properties["trackColor"].GetString(), Is.EqualTo("#ff0000"));
        hub.Update("demo", DataHub.ParseValues("""{"reset":1}""")); session.Refresh();
        gauge = ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Type == "ui.gauge");
        Assert.That(gauge.Properties.ContainsKey("trackColor"), Is.False);
    }

    [TestCase("<text id='t' shadow='maybe'>Text</text>")]
    [TestCase("<text id='t' strokeWidth='11%'>Text</text>")]
    [TestCase("<gauge id='g' trackColor='invalid'/>")]
    public void InvalidPaintSettingsReportTheirElement(string xml)
    {
        var error = Assert.Throws<FormatException>(() => new LayoutRenderer(xml).Render(DataHub.ParseValues("{}")));
        Assert.That(error!.Message, Does.Contain("ID '"));
    }
}
