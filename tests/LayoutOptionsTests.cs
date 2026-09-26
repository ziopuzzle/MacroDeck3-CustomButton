using System.Text.Json;
using MacroDeck.Ui.Model.Nodes;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class LayoutOptionsTests
{
    [Test] public async Task EveryShippedTemplateProducesAValidDisplay()
    {
        foreach (var template in LayoutTemplates.All)
        {
            await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with { Layout = template.Xml, InitialValues = template.InitialValues }, null);
            Assert.That(ButtonTests.Nodes(session.BuildTree().Root).Any(n => n.Id.EndsWith(".message")), Is.False, template.Id);
        }
    }
    private static UiNode Find(UiTree tree, string suffix) => ButtonTests.Nodes(tree.Root).Single(n => n.Id.EndsWith(suffix, StringComparison.Ordinal));

    [TestCase("horizontal")][TestCase("row")]
    public async Task HorizontalChildrenGetWidthsAndExplicitSizingIsPreserved(string direction)
    {
        var xml = $"<stack id=\"row\" direction=\"{direction}\"><text id=\"a\">A</text><bar id=\"b\" value=\"40\"/><stack id=\"c\"><text id=\"label\">C</text></stack><text id=\"fixed\" mainSize=\"20%\">Fixed</text><text id=\"natural\" fill=\"false\">Unit</text></stack>";
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with { Layout = xml }, null);
        foreach (var key in new[] { ".a", ".b", ".c" }) Assert.That(Find(session.BuildTree(), key).Properties["fill"].GetBoolean(), Is.True);
        Assert.That(Find(session.BuildTree(), ".fixed").Properties["mainSize"].GetProperty("basis").GetDouble(), Is.EqualTo(.2));
        Assert.That(Find(session.BuildTree(), ".natural").Properties["fill"].GetBoolean(), Is.False);
        Assert.That(Find(session.BuildTree(), ".row").Properties["direction"].GetString(), Is.EqualTo("horizontal"));
    }

    [Test] public async Task LengthAliasesShortColoursAndSmallFontsKeepUsefulSizes()
    {
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with
        { Layout = "<stack id=\"s\" direction=\"column\" align=\"left\" padding=\"5%\"><text id=\"t\" size=\"4%\" sizeCap=\"11\" color=\"#abc\" mainSize=\"auto\">{{ value:0.0 }}</text></stack>" }, null);
        var text = Find(session.BuildTree(), ".t");
        Assert.That(text.Properties["color"].GetString(), Is.EqualTo("#aabbcc"));
        Assert.That(text.Properties["size"].GetProperty("basis").GetDouble(), Is.EqualTo(.04));
        Assert.That(text.Properties["minSize"].GetProperty("basis").GetDouble(), Is.LessThanOrEqualTo(.04));
        Assert.That(text.Properties.ContainsKey("mainSize"), Is.False);
        Assert.That(text.Properties["text"].GetString(), Is.EqualTo("42.0"));
    }

    [Test] public async Task ConditionsUpdateColoursAndRemoveHiddenElementsWithoutGaps()
    {
        var xml = """
            <stack id="row" direction="horizontal" gap="5%">
              <text id="value" color="#fff">{{value}}<style when="value >= 80" color="#f00"/><style when="value >= 95" color="#ff0"/></text>
              <text id="warning" visibleWhen="value >= 80 and enabled == true">HOT</text>
            </stack>
            """;
        var hub = new DataHub();
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with { Layout = xml }, hub);
        Assert.That(Find(session.BuildTree(), ".row").Children, Has.Count.EqualTo(1));
        Assert.That(Find(session.BuildTree(), ".value").Properties["color"].GetString(), Is.EqualTo("#ffffff"));
        hub.Update("demo", DataHub.ParseValues("{\"value\":85,\"enabled\":true}")); session.Refresh();
        Assert.That(Find(session.BuildTree(), ".row").Children, Has.Count.EqualTo(2));
        Assert.That(Find(session.BuildTree(), ".value").Properties["color"].GetString(), Is.EqualTo("#ff0000"));
        hub.Update("demo", DataHub.ParseValues("{\"value\":99}")); session.Refresh();
        Assert.That(Find(session.BuildTree(), ".value").Properties["color"].GetString(), Is.EqualTo("#ffff00"));
        hub.Update("demo", DataHub.ParseValues("{\"value\":20}")); session.Refresh();
        Assert.That(Find(session.BuildTree(), ".row").Children, Has.Count.EqualTo(1));
        Assert.That(Find(session.BuildTree(), ".value").Properties["color"].GetString(), Is.EqualTo("#ffffff"));
        Assert.That(session.DrainPatches(), Is.Empty, "The snapshot superseded this update.");
    }

    [TestCase("value >= 80 and state == 'ready'", true)]
    [TestCase("(value < 90 or missing) and not false", true)]
    [TestCase("missing == null", true)]
    [TestCase("missing > 0", false)]
    [TestCase("false or true and false", false)]
    [TestCase("!(value == 85)", false)]
    public void ConditionsUseTypedComparisonsAndBooleanPrecedence(string expression, bool expected)
        => Assert.That(DisplayCondition.Evaluate(expression, DataHub.ParseValues("{\"value\":\"85\",\"state\":\"ready\"}")), Is.EqualTo(expected));

    [TestCase("value + 1")][TestCase("f(value)")][TestCase("value >=")]
    public void InvalidConditionsAreRejected(string expression) => Assert.Throws<FormatException>(() => DisplayCondition.Evaluate(expression, new Dictionary<string, JsonElement>()));

    [Test] public async Task EntireRootCanBeHiddenAndShownAgain()
    {
        var hub = new DataHub();
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with { Layout = "<text id=\"label\" visibleWhen=\"show\">Shown</text>" }, hub);
        Assert.That(ButtonTests.Nodes(session.BuildTree().Root).Any(n => n.Id.EndsWith(".label")), Is.False);
        hub.Update("demo", DataHub.ParseValues("{\"show\":true}")); session.Refresh();
        Assert.That(ButtonTests.Text(session.BuildTree(), ".label"), Is.EqualTo("Shown"));
    }

    [TestCase("<text id=\"a\" maxLines=\"1.5\">A</text>")]
    [TestCase("<chart id=\"a\" key=\"value\" points=\"2.5\"/>")]
    public async Task FractionalIntegerOptionsReportErrors(string xml)
    {
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with { Layout = xml }, null);
        Assert.That(ButtonTests.Text(session.BuildTree(), ".message"), Does.Contain("integer"));
    }
}


