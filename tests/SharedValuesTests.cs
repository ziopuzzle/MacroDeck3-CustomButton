using MacroDeck.Plugin.Testing.Fakes;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.ConfigFlow;
using MacroDeck.Sdk.Ui;
using MacroDeck.Ui.Model.Surfaces;
using NUnit.Framework;
using System.Text.Json;

namespace Ziopuzzle.CustomButton.Tests;

public class SharedValuesTests
{
    private static Task<ActionResult> SetShared(CustomButtonIntegration integration, string name, string value, string format = "text")
        => integration.Actions.Single(a => a.Id == "set-shared-value").CreateExecutor().ExecuteAsync(new()
        {
            Parameters = new Dictionary<string, object> { ["key"] = name, ["value"] = value, ["valueType"] = format }
        });

    [Test] public async Task ActionPersistsAThemeChangeAndUpdatesTheOpenDisplay()
    {
        var host = new FakeIntegrationContext();
        var entry = host.Config.AddEntry("Shared display values");
        host.Config.SeedString(entry, SharedValuesFlow.Key, """{"themeAccent":"red","keep":null,"label":{ "名前": "色" }}""");
        var integration = new CustomButtonIntegration();
        await integration.InitializeAsync(host);
        await using var widget = new ButtonSession(ButtonTests.Surface(),
            new("music", "<text id='accent'>{{themeAccent}}</text>", "{}"), integration.Hub);
        Assert.That(ButtonTests.Text(widget.BuildTree(), ".accent"), Is.EqualTo("red"));
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        widget.Changed += (_, _) => changed.TrySetResult();
        Assert.That((await SetShared(integration, "themeAccent", "#54dfcc")).Status, Is.EqualTo(ActionResultStatus.Succeeded));
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.That(ButtonTests.Text(widget.BuildTree(), ".accent"), Is.EqualTo("#54dfcc"));
        Assert.That(integration.Hub.Read("music"), Is.Empty);

        var writes = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(i => Task.Run(() => SetShared(integration, $"theme.{i}", "[1,true]", "json"))));
        Assert.That(writes.All(r => r.Status == ActionResultStatus.Succeeded), Is.True);
        var stored = DataHub.ParseValues((await host.Config.GetStringAsync(entry, SharedValuesFlow.Key))!);
        Assert.That(stored.Count, Is.EqualTo(11));
        Assert.That(stored["keep"].ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(stored["label"].GetRawText(), Is.EqualTo("""{ "名前": "色" }"""));
        var restart = new CustomButtonIntegration();
        await restart.InitializeAsync(host);
        var defaults = restart.Hub.Snapshot("new").SharedValues;
        Assert.That(defaults["themeAccent"].GetString(), Is.EqualTo("#54dfcc"));
        Assert.That(defaults["theme.7"][1].GetBoolean(), Is.True);
        await restart.ShutdownAsync();
        await integration.ShutdownAsync();
    }

    [Test] public async Task ActionExplainsFirstSetupAndRejectedInputsLeaveStoredAndDisplayedValuesIntact()
    {
        var host = new FakeIntegrationContext();
        var integration = new CustomButtonIntegration();
        await integration.InitializeAsync(host);
        Assert.That((await SetShared(integration, "color", "red")).ErrorCode, Is.EqualTo(ActionErrorCodes.NotConfigured));
        var entry = host.Config.AddEntry("Shared display values");
        const string original = """{"color":"blue"}""";
        host.Config.SeedString(entry, SharedValuesFlow.Key, original);
        await integration.ReloadEntriesAsync();
        foreach (var (key, value, type) in new[] { ("bad name", "red", "text"), ("color", "[", "json"), ("color", new string('a', 16000), "text") })
            Assert.That((await SetShared(integration, key, value, type)).ErrorCode, Is.EqualTo(ActionErrorCodes.InvalidParameter));
        Assert.That(await host.Config.GetStringAsync(entry, SharedValuesFlow.Key), Is.EqualTo(original));
        Assert.That(integration.Hub.Snapshot("any").SharedValues["color"].GetString(), Is.EqualTo("blue"));
        await integration.ShutdownAsync();
    }

    [TestCase("""{"themeAccent":"#54dfcc"}""", true)]
    [TestCase("{}", true)]
    [TestCase("[]", false)]
    [TestCase("{", false)]
    [TestCase("""{"themeAccent":1,"themeAccent":2}""", false)]
    public async Task OptionalSettingsValidateTheSubmittedJson(string json, bool valid)
    {
        var integration = new CustomButtonIntegration();
        Assert.That(integration.RequiresConfiguration, Is.False);
        Assert.That(integration.AllowsMultipleConfigurations, Is.False);
        var flow = integration.CreateConfigFlow();
        var step = (await flow.StartAsync(null!, default)).NextStep!;
        var field = step.Fields.Single();
        Assert.That(field.DefaultValue, Is.EqualTo("{}"));
        var result = await flow.SubmitAsync(step.StepId, new Dictionary<string, object?> { [field.Name] = json }, null!, default);
        Assert.That(result.Kind, Is.EqualTo(valid ? ConfigFlowResultKind.Complete : ConfigFlowResultKind.Error));
        if (!valid)
        {
            Assert.That(result.FieldErrors!.ContainsKey(field.Name), Is.True);
            Assert.That(result.NextStep!.Fields.Single().DefaultValue, Is.EqualTo(json));
        }
    }

    [Test] public async Task SavingDefaultsRefreshesOpenChannelsAndNewSessionsReadPersistedSettings()
    {
        var host = new FakeIntegrationContext();
        var integration = new CustomButtonIntegration();
        await integration.InitializeAsync(host);
        const string layout = "<text id='title'>Theme {{accent}}</text>";
        await using var first = new ButtonSession(ButtonTests.Surface(), new("one", layout, "{}"), integration.Hub);
        await using var second = new ButtonSession(ButtonTests.Surface("preview"), new("two", layout, "{}"), integration.Hub);
        var firstChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        first.Changed += (_, _) => firstChanged.TrySetResult();
        second.Changed += (_, _) => secondChanged.TrySetResult();
        var entry = host.Config.AddEntry("Shared display values");
        host.Config.SeedString(entry, SharedValuesFlow.Key, """{"accent":"blue"}""");
        Assert.That(await integration.ReloadEntriesAsync(), Is.True);
        await Task.WhenAll(firstChanged.Task, secondChanged.Task).WaitAsync(TimeSpan.FromSeconds(3));
        foreach (var session in new[] { first, second })
            Assert.That(ButtonTests.Text(session.BuildTree(), ".title"), Is.EqualTo("Theme blue"));
        Assert.That(integration.Hub.Read("one"), Is.Empty);
        var restarted = new CustomButtonIntegration();
        await restarted.InitializeAsync(host);
        await using var later = new ButtonSession(ButtonTests.Surface(), new("new-channel", layout, "{}"), restarted.Hub);
        Assert.That(ButtonTests.Text(later.BuildTree(), ".title"), Is.EqualTo("Theme blue"));
        host.Config.SeedString(entry, SharedValuesFlow.Key, "[]");
        await restarted.ReloadEntriesAsync();
        Assert.That(restarted.Hub.Snapshot("new-channel").SharedValues.ContainsKey("accent"), Is.True);
        host.Config.SeedString(entry, SharedValuesFlow.Key, "{}");
        await restarted.ReloadEntriesAsync();
        Assert.That(restarted.Hub.Snapshot("new-channel").SharedValues, Is.Empty);
        await restarted.ShutdownAsync();
        await integration.ShutdownAsync();
    }

    [Test] public async Task ModalUsesUpdatedSharedValuesIncludingRawNumberFormatting()
    {
        var integration = new CustomButtonIntegration();
        integration.Hub.SetSharedValues("""{"version":1}""");
        var surface = ButtonTests.Surface("dialog") with
        {
            Attributes = new Dictionary<string, JsonElement>
            {
                [UiDialogSurfaceAttributes.ViewId] = JsonSerializer.SerializeToElement(ShowModalAction.ViewId),
                [UiDialogSurfaceAttributes.Data] = JsonSerializer.SerializeToElement(new
                    { channel = "modal", initialValues = "{}", layout = "<text id='number'>{{version}}</text>" })
            }
        };
        await using var session = await integration.CreateSessionAsync(new() { Surface = surface, UiModelVersion = 1 }, default);
        Assert.That(ButtonTests.Text(session!.BuildTree(), ".number"), Is.EqualTo("1"));
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Changed += (_, _) => changed.TrySetResult();
        integration.Hub.SetSharedValues("""{"version":1.00}""");
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.That(ButtonTests.Text(session.BuildTree(), ".number"), Is.EqualTo("1.00"));
    }

    [Test] public void ChannelAndInitialValuesOverrideWholeSharedKeysIncludingNull()
    {
        var hub = new DataHub();
        hub.SetSharedValues("""{"color":"blue","level":42,"theme":{"a":1,"b":2}}""");
        var initial = DataHub.ParseValues("""{"color":"green","theme":{"a":3}}""");
        hub.Update("channel", DataHub.ParseValues("""{"color":null}"""));
        var snapshot = hub.Snapshot("channel");
        var resolved = snapshot.Resolve(initial);
        Assert.That(resolved["color"].ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(resolved["theme"].GetRawText(), Is.EqualTo("""{"a":3}"""));
        Assert.That(resolved["level"].GetInt32(), Is.EqualTo(42));
        hub.Sample("channel", initial);
        Assert.That(hub.ReadHistory("channel", "level", 2), Is.EqualTo(new[] { 42d }));
        hub.Update("channel", DataHub.ParseValues("{}"), replace: true);
        Assert.That(hub.Snapshot("channel").Resolve(initial)["color"].GetString(), Is.EqualTo("green"));
        hub.SetSharedValues("""{"color":"purple"}""");
        Assert.That(hub.Snapshot("channel").Resolve(initial).ContainsKey("level"), Is.False);
        Assert.That(hub.Snapshot("another").Resolve(DataHub.ParseValues("{}"))["color"].GetString(), Is.EqualTo("purple"));
        Assert.That(snapshot.SharedValues["color"].GetString(), Is.EqualTo("blue"));
        Assert.That(hub.Read("channel"), Is.Empty);
    }
}
