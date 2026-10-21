using System.Text.Json;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Variables;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class EditDataActionTests
{
    private static DataHub Hub()
    {
        var hub = new DataHub();
        hub.Update("test", DataHub.ParseValues("""{"playlist":[{"title":"First"},{"title":"Second"}],"settings":{"volume":50,"a.b":1},"keep":42}"""));
        return hub;
    }
    private static ActionExecutionContext Context(params (string, object)[] values)
    {
        var parameters = new Dictionary<string, object> { ["channel"] = "test", ["valueType"] = "text" };
        foreach (var (key, value) in values) parameters[key] = value;
        return new() { OwnerWidgetId = "owner", Parameters = parameters };
    }
    private static Task<ActionResult> Run(DataHub hub, string operation, params (string, object)[] values)
        => (operation == "set" ? (IActionDefinition)new UpdateValueAction(hub) : new EditDisplayDataAction(hub, operation))
            .CreateExecutor().ExecuteAsync(Context(values));

    [Test]
    public async Task SetUsesOneTargetAndKeepsSiblingsAndTextSemantics()
    {
        var hub = Hub();
        Assert.That((await Run(hub, "set", ("key", "playlist[1].title"), ("value", "  日本語  "))).Status, Is.EqualTo(ActionResultStatus.Succeeded));
        Assert.That(DataTarget.Read(hub.Read("test"), "playlist[1].title").GetString(), Is.EqualTo("  日本語  "));
        await Run(hub, "set", ("key", "settings.volume"), ("valueType", "json"), ("value", "75"));
        Assert.That(DataTarget.Read(hub.Read("test"), "settings.volume").GetInt32(), Is.EqualTo(75));
        Assert.That(hub.Read("test")["keep"].GetInt32(), Is.EqualTo(42));
        Assert.That(DataTarget.Read(hub.Read("test"), "playlist[0].title").GetString(), Is.EqualTo("First"));
    }

    [Test]
    public async Task InsertRemoveAndNegativeIndicesWork()
    {
        var hub = Hub();
        await Run(hub, "add", ("key", "playlist"), ("value", "last"));
        await Run(hub, "add", ("key", "playlist"), ("valueType", "json"), ("value", "null"), ("index", "0"));
        Assert.That(hub.Read("test")["playlist"].GetArrayLength(), Is.EqualTo(4));
        await Run(hub, "remove", ("key", "playlist[-1]"));
        await Run(hub, "remove", ("key", "playlist[1].title"));
        Assert.That(hub.Read("test")["playlist"][1].TryGetProperty("title", out _), Is.False);
        await Run(hub, "remove", ("key", "keep"));
        Assert.That(hub.Read("test").ContainsKey("keep"), Is.False);
    }

    [Test]
    public async Task NestedArraysAndQuotedPropertiesWork()
    {
        var hub = Hub();
        await Run(hub, "set", ("key", "settings.list"), ("valueType", "json"), ("value", "[]"));
        await Run(hub, "add", ("key", "settings.list"), ("value", "x"));
        await Run(hub, "remove", ("key", """settings["a.b"]"""));
        Assert.That(DataTarget.Read(hub.Read("test"), "settings.list[0]").GetString(), Is.EqualTo("x"));
        Assert.That(hub.Read("test")["settings"].TryGetProperty("a.b", out _), Is.False);
    }

    [Test]
    public async Task ExistingDottedTopLevelNameWinsAndQuotedRootDisambiguates()
    {
        var hub = Hub();
        hub.Update("test", DataHub.ParseValues("""{"settings.volume":"old"}"""));
        await Run(hub, "set", ("key", "settings.volume"), ("value", "literal"));
        await Run(hub, "set", ("key", """["settings"].volume"""), ("value", "nested"));
        Assert.That(hub.Read("test")["settings.volume"].GetString(), Is.EqualTo("literal"));
        Assert.That(hub.Read("test")["settings"].GetProperty("volume").GetString(), Is.EqualTo("nested"));
    }

    [Test]
    public async Task LegacySetCreatesLiteralDottedNamesAndKeepsValuesAsText()
    {
        var hub = new DataHub();
        var result = await new UpdateValueAction(hub).CreateExecutor().ExecuteAsync(new ActionExecutionContext {
            Parameters = new Dictionary<string, object> { ["channel"] = "test", ["key"] = "a.b", ["value"] = 42 }
        });
        Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Succeeded));
        Assert.That(hub.Read("test")["a.b"].GetString(), Is.EqualTo("42"));
    }

    [TestCase("add", "missing", "x")]
    [TestCase("add", "settings", "x")]
    [TestCase("set", "playlist[99].title", "x")]
    [TestCase("set", "settings.missing.child", "x")]
    [TestCase("remove", "settings.missing", "")]
    [TestCase("remove", "playlist[", "")]
    [TestCase("remove", "playlist[0]tail", "")]
    [TestCase("remove", "playlist[1.5]", "")]
    [TestCase("remove", """settings["unfinished]""", "")]
    public async Task InvalidTargetsDoNotChangeData(string operation, string key, string value)
    {
        var hub = Hub(); var before = JsonSerializer.Serialize(hub.Read("test")); var revision = hub.Revision("test");
        Assert.That((await Run(hub, operation, ("key", key), ("value", value))).Status, Is.EqualTo(ActionResultStatus.Failed));
        Assert.That(JsonSerializer.Serialize(hub.Read("test")), Is.EqualTo(before));
        Assert.That(hub.Revision("test"), Is.EqualTo(revision));
    }

    [Test]
    public async Task InvalidJsonAndOversizeEditsDoNotChangeData()
    {
        var hub = Hub(); var revision = hub.Revision("test");
        Assert.That((await Run(hub, "set", ("key", "playlist"), ("valueType", "json"), ("value", "{broken"))).Status, Is.EqualTo(ActionResultStatus.Failed));
        Assert.That((await Run(hub, "add", ("key", "playlist"), ("value", new string('x', 16000)))).Status, Is.EqualTo(ActionResultStatus.Failed));
        Assert.That(hub.Revision("test"), Is.EqualTo(revision));
    }

    [TestCase("add")][TestCase("remove")]
    public async Task OldExperimentalParametersAreRejectedRatherThanEditingTheWrongTarget(string operation)
    {
        var hub = Hub(); var revision = hub.Revision("test");
        Assert.That((await Run(hub, operation, ("key", "playlist"), ("path", "/0"), ("value", "x"))).Status, Is.EqualTo(ActionResultStatus.Failed));
        Assert.That(hub.Revision("test"), Is.EqualTo(revision));
    }

    [Test]
    public async Task ConcurrentAppendsKeepEveryItem()
    {
        var hub = Hub();
        await Task.WhenAll(Enumerable.Range(0, 30).Select(i => Task.Run(() => Run(hub, "add", ("key", "playlist"), ("value", i.ToString())))));
        Assert.That(hub.Read("test")["playlist"].GetArrayLength(), Is.EqualTo(32));
    }

    [TestCase(UserVariableWriteStatus.Applied)]
    [TestCase(UserVariableWriteStatus.NotFound)]
    public async Task PopWritesBeforeRemovingAndPreservesDataOnRejection(UserVariableWriteStatus status)
    {
        var hub = Hub(); var api = new Variables { Status = status };
        var action = new EditDisplayDataAction(hub, "remove", () => api).CreateExecutor();
        var result = await action.ExecuteAsync(Context(("key", "playlist[-1]"), ("variable", "output")));
        Assert.That(api.Value, Is.EqualTo("""{"title":"Second"}"""));
        Assert.That(api.Owner, Is.EqualTo("owner"));
        Assert.That(result.Status, Is.EqualTo(status == UserVariableWriteStatus.Applied ? ActionResultStatus.Succeeded : ActionResultStatus.Failed));
        Assert.That(hub.Read("test")["playlist"].GetArrayLength(), Is.EqualTo(status == UserVariableWriteStatus.Applied ? 1 : 2));
    }

    [Test]
    public async Task PopDoesNotRemoveChangedSourceDuringVariableWrite()
    {
        var hub = Hub();
        var api = new Variables { DuringWrite = () => hub.Update("test", DataHub.ParseValues("""{"playlist":["replacement"]}""")) };
        var result = await new EditDisplayDataAction(hub, "remove", () => api).CreateExecutor()
            .ExecuteAsync(Context(("key", "playlist[-1]"), ("variable", "output")));
        Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Failed));
        Assert.That(hub.Read("test")["playlist"][0].GetString(), Is.EqualTo("replacement"));
    }

    [Test]
    public void ActionCatalogGroupsEditingThenTransfersAndModalWithoutRedundantSet()
    {
        Assert.That(new CustomButtonIntegration().Actions.Select(a => a.Id), Is.EqualTo(new[] {
            "set-value", "set-data", "add-array-item", "remove-data-part", "copy-value", "copy-data", "write-variable", "show-modal" }));
        foreach (var action in new IActionDefinition[] { new UpdateValueAction(Hub()), new EditDisplayDataAction(Hub(), "add"), new EditDisplayDataAction(Hub(), "remove") })
        {
            Assert.That(action.Parameters.Any(p => p.Name == "sourceWidgetId"), Is.False);
            Assert.That(action.Parameters.Single(p => p.Name == "key").DefaultValue, Is.Null);
        }
    }

    private sealed class Variables : IUserVariableApi
    {
        public string? Value, Owner;
        public Action? DuringWrite;
        public UserVariableWriteStatus Status = UserVariableWriteStatus.Applied;
        public Task<UserVariableWriteResult> ApplyAsync(string name, string? ownerWidgetId, UserVariableOperation operation, string? value, CancellationToken cancellationToken = default)
        {
            Value = value; Owner = ownerWidgetId; DuringWrite?.Invoke();
            return Task.FromResult(new UserVariableWriteResult(Status, "Host detail"));
        }
    }
}
