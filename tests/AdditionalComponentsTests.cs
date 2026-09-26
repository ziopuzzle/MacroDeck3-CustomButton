using System.Text.Json;
using MacroDeck.Ui.Model.Nodes;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class AdditionalComponentsTests
{
    private static UiNode Find(ButtonSession session, string id) => ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id.EndsWith("." + id));

    [Test] public async Task ClockUsesClientTimeWithoutPatchesAndAcceptConditionalColours()
    {
        var hub = new DataHub();
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with { Layout = """
            <stack id="s"><clock id="dial" zone="{{zone}}"><style when="value >= 80" color="#f00"/></clock>
            </stack>
            """, InitialValues = "{\"zone\":\"Asia/Tokyo\"}" }, hub);
        Assert.That(Find(session, "dial").Properties["value"].GetProperty("$time").GetProperty("zone").GetString(), Is.EqualTo("Asia/Tokyo"));
        session.Refresh(); Assert.That(session.DrainPatches(), Is.Empty);
        hub.Update("demo", DataHub.ParseValues("{\"value\":85}")); session.Refresh();
        Assert.That(Find(session, "dial").Properties["color"].GetString(), Is.EqualTo("#ff0000"));
    }

    [Test] public async Task ProgressPreservesAnchorAcrossUnrelatedUpdates()
    {
        var hub = new DataHub();
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with { Layout = """
            <stack id="s">
              <progress-bar id="bar" positionMs="{{position}}" durationMs="100000" anchor="{{anchor}}" rate="{{rate}}"/>
            </stack>
            """, InitialValues = """{"position":42000,"anchor":"2026-09-13T12:00:00+09:00","rate":1} """ }, hub);
        var reference = Find(session, "bar").Properties["value"].GetRawText();
        hub.Update("demo", DataHub.ParseValues("{\"unrelated\":5}")); session.Refresh();
        Assert.That(session.DrainPatches(), Is.Empty);
        Assert.That(Find(session, "bar").Properties["value"].GetRawText(), Is.EqualTo(reference));
        hub.Update("demo", DataHub.ParseValues("{\"rate\":0,\"position\":45000}")); session.Refresh();
        var progress = Find(session, "bar").Properties["value"].GetProperty("$progress");
        Assert.That(progress.GetProperty("rate").GetDouble(), Is.Zero);
        Assert.That(progress.GetProperty("positionMs").GetInt64(), Is.EqualTo(45000));
    }

    [Test] public async Task SliderNormalizesRange()
    {
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with { Layout = """
            <stack id="s" direction="horizontal"><slider id="meter" value="0" min="-50" max="50" direction="vertical"/>
            </stack>
            """ }, null);
        Assert.That(Find(session, "meter").Properties["level"].GetDouble(), Is.EqualTo(.5));
        Assert.That(Find(session, "meter").Properties["direction"].GetString(), Is.EqualTo("vertical"));
    }

    [TestCase("<progress-bar id='x' rate='1'/>", "anchor")]
    [TestCase("<progress-bar id='x' anchor='2026-09-13T12:00:00'/>", "anchor")]
    [TestCase("<progress-bar id='x' positionMs='1.5'/>", "integer")]
    [TestCase("<slider id='x' min='100' max='100'/>", "max")]
    public async Task InvalidOptionsProduceAnExplanationInsteadOfAnInvisibleComponent(string xml, string expected)
    {
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with { Layout = xml }, null);
        Assert.That(LocalizationTests.Resolve(Find(session, "message").Properties["text"]), Does.Contain(expected));
    }
}


