using System.Text.Json;
using System.Text.Json.Nodes;
using MacroDeck.Ui.Model.Events;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class CopiedWidgetTests
{
    [TestCase("source", "test-widget")]
    [TestCase("demo", "demo")]
    [TestCase("music", "music")]
    public async Task AutomaticChannelsFollowCopiesAndMatchingNestedActions(string channel, string expected)
    {
        var flows = ActionEditorDefaults.ChangeChannel(Flows(), "music", channel);
        await using var session = new ConfigurationSession(ButtonTests.Surface("config"), TestLayouts.Sample with
            { Channel = channel, ConfigurationWidgetId = "source", Flows = flows });
        Assert.That(Value(session, "channel").GetString(), Is.EqualTo(expected));
        var loop = Value(session, "flows")[0].GetProperty("children")[0];
        Assert.That(loop.GetProperty("children")[0].GetProperty("parameters")[0].GetProperty("value").GetString(), Is.EqualTo(expected));
        var branches = loop.GetProperty("branches")[0].GetProperty("children");
        Assert.That(branches[0].GetProperty("parameters")[0].GetProperty("value").GetString(), Is.EqualTo(expected));
        Assert.That(branches[1].GetProperty("parameters")[0].GetProperty("value").GetString(), Is.EqualTo("other"));
        Assert.That(branches[2].GetProperty("parameters")[0].GetProperty("value").GetString(), Is.EqualTo("music"));
    }

    [Test] public async Task NewWidgetChannelIsPersistedByInitialCorrection()
    {
        await using var session = new ConfigurationSession(ButtonTests.Surface("config"), ButtonSettings.Read(JsonSerializer.Deserialize<JsonElement>(ButtonSettings.DefaultData)));
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Changed += (_, _) => changed.TrySetResult();
        Assert.That(Value(session, "channel").GetString(), Is.EqualTo("test-widget"));
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var operations = session.DrainPatches().SelectMany(p => p.Operations).ToArray();
        Assert.That(operations.Single(p => p.NodeId == "channel").Properties!["value"].GetString(), Is.EqualTo("test-widget"));
        Assert.That(operations.Single(p => p.NodeId == "configurationWidgetId").Properties!["value"].GetString(), Is.EqualTo("test-widget"));
    }
    private static JsonElement Flows() => JsonSerializer.SerializeToElement(JsonNode.Parse("""
    [{"triggerType":"onEvent","event":{"providerId":"net.ziopuzzle.custombutton","eventId":"element-press","parameters":[{"name":"widgetId","value":"source"},{"name":"elementId","value":"play"}]},"children":[{"id":"loop","children":[{"id":"a","integrationId":"net.ziopuzzle.custombutton","actionId":"set-value","parameters":[{"name":"channel","value":"music"}]}],"branches":[{"children":[{"id":"b","integrationId":"net.ziopuzzle.custombutton","actionId":"set-data","parameters":[{"name":"channel","value":"music"}]},{"id":"c","integrationId":"net.ziopuzzle.custombutton","actionId":"set-value","parameters":[{"name":"channel","value":"other"}]},{"id":"d","integrationId":"other","actionId":"set-value","parameters":[{"name":"channel","value":"music"}]}]}]}]},
    {"triggerType":"onEvent","event":{"providerId":"net.ziopuzzle.custombutton","eventId":"short-press","parameters":[{"name":"widgetId","value":"other-widget"}]},"children":[]},
    {"triggerType":"onEvent","event":{"providerId":"another-plugin","eventId":"event","parameters":[{"name":"target","type":"widget-target","value":"source","valueLabel":"Old name"},{"name":"variable","value":"source"}]},"children":[]}]
    """));
    private static JsonElement Value(ConfigurationSession session, string name) => ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id == name).Properties["value"];

    [Test]
    public async Task OpeningACopyPublishesALaterRevisionWithoutUserInteraction()
    {
        await using var session = new ConfigurationSession(ButtonTests.Surface("config"), TestLayouts.Sample with { Flows = Flows(), ConfigurationWidgetId = "source" });
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Changed += (_, _) => changed.TrySetResult();
        var initial = session.BuildTree();
        Assert.That(session.DrainPatches(), Is.Empty);
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var patches = session.DrainPatches();
        Assert.That(patches, Has.Count.EqualTo(1));
        Assert.That(patches[0].FromRevision, Is.EqualTo(initial.Revision));
        Assert.That(patches[0].ToRevision, Is.EqualTo(initial.Revision + 1));
        var flows = patches[0].Operations.Single(p => p.NodeId == "flows").Properties!["value"];
        Assert.That(flows[0].GetProperty("event").GetProperty("parameters")[0].GetProperty("value").GetString(), Is.EqualTo("test-widget"));
        Assert.That(session.BuildTree().Revision, Is.EqualTo(patches[0].ToRevision));
        Assert.That(session.DrainPatches(), Is.Empty);
        session.Dispatch(new UiEvent { NodeId = "channel", Name = "change", Revision = patches[0].ToRevision, Data = JsonSerializer.SerializeToElement("next") });
        Assert.That(Value(session, "channel").GetString(), Is.EqualTo("next"));
        Assert.That(session.DrainPatches()[0].FromRevision, Is.EqualTo(patches[0].ToRevision));
    }

    [Test]
    public async Task ClosingBeforeTheInitialCorrectionDoesNotNotify()
    {
        var session = new ConfigurationSession(ButtonTests.Surface("config"), TestLayouts.Sample);
        var notifications = 0;
        session.Changed += (_, _) => Interlocked.Increment(ref notifications);
        session.BuildTree();
        await session.DisposeAsync();
        await Task.Delay(600);
        Assert.That(notifications, Is.Zero);
    }
    [TestCase("source", "test-widget")]
    [TestCase("", "source")]
    [TestCase("test-widget", "source")]
    public async Task ReopeningOnlyRemapsKnownSourceTargets(string storedOwner, string expectedTarget)
    {
        var original = Flows();
        await using var session = new ConfigurationSession(ButtonTests.Surface("config"), TestLayouts.Sample with { Flows = original, ConfigurationWidgetId = storedOwner });
        var next = Value(session, "flows");
        Assert.That(next[0].GetProperty("event").GetProperty("parameters")[0].GetProperty("value").GetString(), Is.EqualTo(expectedTarget));
        Assert.That(next[0].GetProperty("event").GetProperty("parameters")[1].GetProperty("value").GetString(), Is.EqualTo("play"));
        Assert.That(next[1].GetProperty("event").GetProperty("parameters")[0].GetProperty("value").GetString(), Is.EqualTo("other-widget"));
        Assert.That(next[2].GetProperty("event").GetProperty("parameters")[0].GetProperty("value").GetString(), Is.EqualTo(expectedTarget));
        Assert.That(next[2].GetProperty("event").GetProperty("parameters")[1].GetProperty("value").GetString(), Is.EqualTo("source"));
        Assert.That(next[0].GetProperty("children").GetRawText(), Is.EqualTo(original[0].GetProperty("children").GetRawText()));
        Assert.That(Value(session, "configurationWidgetId").GetString(), Is.EqualTo("test-widget"));
    }

    [Test]
    public async Task ChannelEditsFollowMatchingNestedActionsAndPreserveOtherDestinations()
    {
        await using var session = new ConfigurationSession(ButtonTests.Surface("config"), TestLayouts.Sample with { Channel = "music", Flows = Flows() });
        session.Dispatch(new UiEvent { NodeId = "channel", Name = "change", Data = JsonSerializer.SerializeToElement("player") });
        var loop = Value(session, "flows")[0].GetProperty("children")[0];
        Assert.That(loop.GetProperty("children")[0].GetProperty("parameters")[0].GetProperty("value").GetString(), Is.EqualTo("player"));
        var branches = loop.GetProperty("branches")[0].GetProperty("children");
        Assert.That(branches[0].GetProperty("parameters")[0].GetProperty("value").GetString(), Is.EqualTo("player"));
        Assert.That(branches[1].GetProperty("parameters")[0].GetProperty("value").GetString(), Is.EqualTo("other"));
        Assert.That(branches[2].GetProperty("parameters")[0].GetProperty("value").GetString(), Is.EqualTo("music"));
        session.Dispatch(new UiEvent { NodeId = "channel", Name = "change", Data = JsonSerializer.SerializeToElement("music") });
        Assert.That(Value(session, "flows")[0].GetProperty("children").GetRawText(), Is.EqualTo(Flows()[0].GetProperty("children").GetRawText()));
    }

    [Test]
    public void ProvenanceIsOptionalAndSurvivesSettingsRead()
    {
        Assert.That(ButtonSettings.Read(JsonSerializer.SerializeToElement(new { configurationWidgetId = "source" })).ConfigurationWidgetId, Is.EqualTo("source"));
        Assert.That(ButtonSettings.Read(JsonSerializer.SerializeToElement(new { })).ConfigurationWidgetId, Is.Empty);
        Assert.That(JsonDocument.Parse(ButtonSettings.Schema).RootElement.GetProperty("properties").TryGetProperty("configurationWidgetId", out _), Is.True);
    }
}
