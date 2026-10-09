using System.Text.Json;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class ElementEventReferenceTests
{
    private const string Original = "<stack id='root'><stack id='play' interactive='true'><text id='label'>Play</text></stack></stack>";
    private static JsonElement Flows() => JsonSerializer.SerializeToElement(new[] { "test-widget", "other-widget" }.Select(target => new
    {
        triggerType = "onEvent",
        @event = new { providerId = CustomButtonIntegration.PluginId, eventId = "element-press", parameters = new[] {
            new { name = "widgetId", value = target }, new { name = "elementId", value = "play" } } },
        children = new[] { new { id = "action", integrationId = CustomButtonIntegration.PluginId, actionId = "set-value",
            parameters = new[] { new { name = "value", value = "play" } } } }
    }));
    private static string Target(ConfigurationSession session, int index = 0) => ButtonTests.Nodes(session.BuildTree().Root)
        .Single(n => n.Id == "flows").Properties["value"][index].GetProperty("event").GetProperty("parameters")[1].GetProperty("value").GetString()!;
    private static void Edit(ConfigurationSession session, string xml) => session.Dispatch(new() { NodeId = "layout", Name = "change", Data = JsonSerializer.SerializeToElement(xml) });

    [Test] public async Task RenameFollowsSelfOnlyAcrossIncompleteXmlAndUndo()
    {
        await using var session = new ConfigurationSession(ButtonTests.Surface("config"), TestLayouts.Sample with { Layout = Original, Flows = Flows() });
        Edit(session, "<stack");
        Assert.That(Target(session), Is.EqualTo("play"));
        Edit(session, Original.Replace("id='play'", "id='playPause'"));
        Assert.That(Target(session), Is.EqualTo("playPause"));
        Assert.That(Target(session, 1), Is.EqualTo("play"));
        var flows = ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id == "flows").Properties["value"];
        Assert.That(flows[0].GetProperty("event").GetProperty("eventName").GetString(), Does.EndWith("playPause"));
        Assert.That(flows[0].GetProperty("children")[0].GetProperty("parameters")[0].GetProperty("value").GetString(), Is.EqualTo("play"));
        Edit(session, Original);
        Assert.That(Target(session), Is.EqualTo("play"));
    }

    [TestCase("<stack id='root'/>")]
    [TestCase("<stack id='root'><stack id='next' interactive='true'><text id='label'>Next</text></stack></stack>")]
    public async Task DeletionOrReplacementWarnsWithoutGuessing(string xml)
    {
        await using var session = new ConfigurationSession(ButtonTests.Surface("config"), TestLayouts.Sample with { Layout = Original, Flows = Flows() });
        Edit(session, xml);
        Assert.That(Target(session), Is.EqualTo("play"));
        var warning = ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id.EndsWith(".elementReferenceWarnings"));
        Assert.That(LocalizationTests.Resolve(warning.Properties["text"]), Does.Contain("'play' is not a target"));
    }
}
