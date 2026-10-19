using System.Text.Json;
using MacroDeck.Ui.Runtime;
using MacroDeck.Ui.Model.Nodes;
using MacroDeck.Plugin.Testing.Fakes;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class RepeatTests
{
    private static UiNode Render(string xml, string json) => new UiView(ButtonTests.Surface(), new LayoutRenderer(xml).Render(DataHub.ParseValues(json))).Tree.Root;
    private static string[] Texts(UiNode node) => ButtonTests.Nodes(node).Where(n => n.Type == "ui.text").Select(n => n.Properties["text"].GetString()!).ToArray();
    private const string Rows = "<repeat id='tracks' items='{{tracks}}' itemKey='id'><stack id='row' interactive='true'><text id='title'>{{item.title}}</text></stack></repeat>";

    [Test]
    public void ObjectsConditionsAndCalculationsUsePrivateScopes()
    {
        var root = Render("<stack id='root'><repeat id='rows' items='tracks' itemKey='id'><text id='text' visibleWhen='item.show == true'>{{prefix}} {{= index + 1}} {{item.meta.title}} {{itemKey}}</text></repeat><text id='outside'>{{item}}</text></stack>",
            """{"prefix":"Song","item":"outside","tracks":[{"id":"a","show":true,"meta":{"title":"A"}},{"id":"b","show":false,"meta":{"title":"B"}},{"id":"c","show":true,"meta":{"title":"C"}}]}""");
        Assert.That(Texts(root), Is.EqualTo(new[] { "Song 1 A a", "Song 3 C c", "outside" }));
    }

    [Test]
    public void KeyedNodesKeepIdentityAcrossReorderAndRemoval()
    {
        var before = Render(Rows, """{"tracks":[{"id":"a","title":"A"},{"id":"b","title":"B"}]}""");
        var after = Render(Rows, """{"tracks":[{"id":"b","title":"B"},{"id":"a","title":"A"},{"id":"c","title":"C"}]}""");
        var oldIds = ButtonTests.Nodes(before).Where(n => n.Type == "ui.text").ToDictionary(n => n.Properties["text"].GetString()!, n => n.Id);
        foreach (var node in ButtonTests.Nodes(after).Where(n => n.Type == "ui.text" && oldIds.ContainsKey(n.Properties["text"].GetString()!)))
            Assert.That(node.Id, Is.EqualTo(oldIds[node.Properties["text"].GetString()!]));
        Assert.That(ButtonTests.Nodes(after).Select(n => n.Id).Distinct().Count(), Is.EqualTo(ButtonTests.Nodes(after).Count()));
        Assert.That(Texts(Render(Rows, """{"tracks":[{"id":"b","title":"B"}]}""")), Is.EqualTo(new[] { "B" }));
    }

    [Test]
    public void PrimitiveAndNestedArraysRender()
    {
        var root = Render("<repeat id='groups' items='groups' itemKey='id'><repeat id='entries' items='{{item.values}}'><text id='value'>{{index}}:{{item}}</text></repeat></repeat>",
            """{"groups":[{"id":"a","values":["X",2,true]},{"id":"b","values":["Y"]}]}""");
        Assert.That(Texts(root), Is.EqualTo(new[] { "0:X", "1:2", "2:True", "0:Y" }));
        Assert.That(ButtonTests.Nodes(root).Select(n => n.Id).Distinct().Count(), Is.EqualTo(ButtonTests.Nodes(root).Count()));
    }

    [TestCase("{}")] [TestCase("{\"tracks\":null}")] [TestCase("{\"tracks\":[]}")]
    public void MissingNullAndEmptyArraysProduceEmptyContainer(string json)
        => Assert.That(Texts(Render(Rows, json)), Is.Empty);

    [TestCase("{\"tracks\":42}", "JSON array")]
    [TestCase("{\"tracks\":[{\"id\":\"a\"},{\"id\":\"a\"}]}", "Duplicate repeat item key")]
    [TestCase("{\"tracks\":[{\"title\":\"A\"}]}", "itemKey")]
    public void InvalidDataHasLocatedErrors(string json, string message)
    {
        var error = Assert.Throws<FormatException>(() => Render(Rows, json));
        Assert.That(error!.Message, Does.Contain(message).And.Contain("ID 'tracks'").And.Contain("line"));
    }

    [Test]
    public void ExpansionAndItemCountsAreBounded()
    {
        var many = JsonSerializer.Serialize(new { tracks = Enumerable.Range(0, 257).ToArray() });
        Assert.That(Assert.Throws<FormatException>(() => Render("<repeat id='rows' items='tracks'><text id='text'>{{item}}</text></repeat>", many))!.Message, Does.Contain("256 items"));
        var nested = "<repeat id='outer' items='items'><repeat id='inner' items='items'><text id='text'>{{item}}</text></repeat></repeat>";
        var data = JsonSerializer.Serialize(new { items = Enumerable.Range(0, 50).ToArray() });
        Assert.That(Assert.Throws<FormatException>(() => Render(nested, data))!.Message, Does.Contain("Expanded layout exceeds"));
    }

    [Test]
    public void ArrayDataSurvivesHubUpdatesAndCannotGrowWithoutBound()
    {
        var hub = new DataHub();
        hub.Update("data", DataHub.ParseValues("""{"tracks":[{"id":"a","title":"A"}]}"""));
        Assert.That(hub.Snapshot("data").Values["tracks"][0].GetProperty("title").GetString(), Is.EqualTo("A"));
        Assert.Throws<FormatException>(() => hub.Update("data", new Dictionary<string, JsonElement> { ["tracks"] = JsonSerializer.SerializeToElement(new[] { new string('x', 16001) }) }));
        Assert.That(hub.Snapshot("data").Values["tracks"][0].GetProperty("title").GetString(), Is.EqualTo("A"));
    }

    [Test]
    public async Task RepeatedPressPublishesTemplateIdAndCurrentItemAfterReorder()
    {
        var integration = new CustomButtonIntegration(); var host = new FakeIntegrationContext();
        await integration.InitializeAsync(host);
        var surface = ButtonTests.Surface();
        surface = surface with { Attributes = new Dictionary<string, JsonElement>(surface.Attributes) {
            ["data"] = JsonSerializer.SerializeToElement(new { channel = "repeat-test", layout = Rows,
                initialValues = """{"tracks":[{"id":"a","title":"A"},{"id":"b","title":"B"}]}""" }) } };
        await using var session = (ButtonSession)(await integration.CreateSessionAsync(new() { Surface = surface, UiModelVersion = 1 }, default))!;
        var root = session.BuildTree().Root;
        var row = ButtonTests.Nodes(root).Single(n => n.Type == "ui.stack" && n.Children.Any(c => c.Type == "ui.text" && c.Properties["text"].GetString() == "B"));
        session.Dispatch(new() { NodeId = row.Id, Name = "press" });
        var payload = host.Events.Published.Last(e => e.EventId == "element-press").Parameters!.Value;
        Assert.That(payload.GetProperty("elementId").GetString(), Is.EqualTo("row"));
        Assert.That(payload.GetProperty("repeatId").GetString(), Is.EqualTo("tracks"));
        Assert.That(payload.GetProperty("itemKey").GetString(), Is.EqualTo("b"));
        Assert.That(payload.GetProperty("itemIndex").GetInt32(), Is.EqualTo(1));
        Assert.That(payload.GetProperty("itemJson").GetString(), Does.Contain("\"title\":\"B\""));
        integration.Hub.Update("repeat-test", DataHub.ParseValues("""{"tracks":[{"id":"b","title":"Changed"}]}"""));
        session.Refresh(); session.BuildTree();
        session.Dispatch(new() { NodeId = row.Id, Name = "press" });
        payload = host.Events.Published.Last(e => e.EventId == "element-press").Parameters!.Value;
        Assert.That(payload.GetProperty("itemIndex").GetInt32(), Is.Zero);
        Assert.That(payload.GetProperty("itemJson").GetString(), Does.Contain("Changed"));
    }

    [Test]
    public void ItemControlsAreReadOnlyButSeparateChannelKeysWork()
    {
        var xml = "<repeat id='rows' items='items'><slider id='slider' key='item.volume' interactive='true'/></repeat>";
        var json = """{"items":[{"volume":5}]}""";
        Assert.That(Assert.Throws<FormatException>(() => Render(xml, json))!.Message, Does.Contain("read-only"));
        Assert.DoesNotThrow(() => Render(xml.Replace("item.volume", "volume_{{index}}"), json));
    }

    [Test]
    public void EditorSupportsRepeatAndKeepsTemplateIds()
    {
        var document = new LayoutDocument("<stack id='root'/>");
        var repeat = document.Add("root", "repeat"); var text = document.Add(repeat, "text");
        document.SetText(text, "{{item.title}}");
        Assert.That(document.Serialize(), Does.Contain("items=\"{{items}}\""));
        Assert.That(LayoutRenderer.RepeatPreviewKeys(document.Serialize(), text, DataHub.ParseValues("""{"items":[{"title":"A"}]}""")), Does.Contain("item.title"));
        Assert.That(ButtonEvents.InputTargets(Rows).Select(t => t.Id), Does.Contain("row"));
    }
}
