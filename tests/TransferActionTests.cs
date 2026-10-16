using System.Text.Json;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Variables;
using MacroDeck.Sdk.Widgets;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class TransferActionTests
{
    private static Task<WidgetDataSnapshot?> Initial(string id, CancellationToken ct) => Task.FromResult<WidgetDataSnapshot?>(
        new(id, CustomButtonIntegration.TypeId, JsonSerializer.SerializeToElement(new
        { channel = "source", initialValues = "{\"value1\":\"a\",\"value2\":\"b\"}" })));

    [Test]
    public async Task InitialJsonCopiesWithoutAnyLiveDataAndLiveOverridesOnlyMatchingKeys()
    {
        var hub = new DataHub();
        var copy = new CopyDisplayDataAction(hub, Initial).CreateExecutor();
        var ctx = Context(("channel", "source"), ("destinationChannel", "target"));
        Assert.That((await copy.ExecuteAsync(ctx)).Status, Is.EqualTo(ActionResultStatus.Succeeded));
        Assert.That(hub.Read("target")["value1"].GetString(), Is.EqualTo("a"));
        Assert.That(hub.Read("target")["value2"].GetString(), Is.EqualTo("b"));
        hub.Update("source", DataHub.ParseValues("{\"value1\":null,\"value3\":3}"));
        await copy.ExecuteAsync(ctx);
        Assert.That(hub.Read("target")["value1"].ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(hub.Read("target")["value2"].GetString(), Is.EqualTo("b"));
        Assert.That(hub.Read("target")["value3"].GetInt32(), Is.EqualTo(3));
    }

    [Test]
    public async Task ExplicitWidgetProvidesInitialValuesForSingleCopyAndVariableWrite()
    {
        var hub = new DataHub();
        var ctx = Context(("channel", "source"), ("sourceWidgetId", "another"), ("key", "value1"),
            ("destinationChannel", "target"), ("destinationKey", "value3"), ("variable", "output"));
        var copy = new CopyDisplayValueAction(hub, Initial).CreateExecutor();
        Assert.That((await copy.ExecuteAsync(ctx)).Status, Is.EqualTo(ActionResultStatus.Succeeded));
        Assert.That(hub.Read("target")["value3"].GetString(), Is.EqualTo("a"));
        var api = new Variables();
        await new WriteDisplayVariableAction(hub, () => api, Initial).CreateExecutor().ExecuteAsync(ctx);
        Assert.That(api.Last, Is.EqualTo(("output", "owner", "a")));
    }

    [TestCase("")][TestCase("selected")]
    public async Task DoesNotMixInitialValuesFromAnotherChannel(string selected)
    {
        var hub = new DataHub(); hub.Update("other", DataHub.ParseValues("{\"v\":1}"));
        var result = await new CopyDisplayDataAction(hub, Initial).CreateExecutor().ExecuteAsync(Context(
            ("channel", "other"), ("sourceWidgetId", selected), ("destinationChannel", "target")));
        if (selected.Length > 0)
        {
            Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Failed));
            Assert.That(hub.Read("target"), Is.Empty);
        }
        else
        {
            Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Succeeded));
            Assert.That(hub.Read("target").Keys, Is.EquivalentTo(new[] { "v" }));
        }
    }

    [TestCase(false, "widget")][TestCase(true, "widget")]
    [TestCase(false, "preview")][TestCase(true, "preview")]
    public async Task CopyWakesReceivingRendererWithoutManualRefresh(bool wholeChannel, string kind)
    {
        var hub = new DataHub();
        hub.Update("source", DataHub.ParseValues("{\"value\":73}"));
        await using var target = new ButtonSession(ButtonTests.Surface(kind), TestLayouts.Sample with { Channel = "target" }, hub);
        target.BuildTree();
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        target.Changed += (_, _) => changed.TrySetResult();
        IActionDefinition action = wholeChannel ? new CopyDisplayDataAction(hub) : new CopyDisplayValueAction(hub);
        var result = await action.CreateExecutor().ExecuteAsync(Context(("channel", "source"), ("key", "value"), ("destinationChannel", "target")));
        Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Succeeded));
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.That(target.DrainPatches(), Is.Not.Empty);
        Assert.That(ButtonTests.Text(target.BuildTree(), ".value"), Is.EqualTo("73%"));
    }

    private static ActionExecutionContext Context(params (string, object)[] values) => new()
    { OwnerWidgetId = "owner", Parameters = values.ToDictionary(p => p.Item1, p => p.Item2) };

    [TestCase("42")][TestCase("true")][TestCase("null")][TestCase("\"  日本語\\ntext  \"")]
    public async Task CopiesValueWithoutChangingTypeOrOtherValues(string json)
    {
        var hub = new DataHub(); hub.Update("source", DataHub.ParseValues("{\"v\":" + json + "}"));
        hub.Update("target", DataHub.ParseValues("{\"keep\":1}"));
        var result = await new CopyDisplayValueAction(hub).CreateExecutor().ExecuteAsync(Context(
            ("channel", "source"), ("key", "v"), ("destinationChannel", "target"), ("destinationKey", "renamed")));
        Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Succeeded));
        Assert.That(hub.Read("target")["renamed"].GetRawText(), Is.EqualTo(hub.Read("source")["v"].GetRawText()));
        Assert.That(hub.Read("target")["keep"].GetInt32(), Is.EqualTo(1));
    }

    [TestCase("merge", 3)][TestCase("replace", 2)]
    public async Task CopiesSnapshotWithSelectedMode(string mode, int count)
    {
        var hub = new DataHub(); hub.Update("source", DataHub.ParseValues("{\"v\":42,\"title\":\"test\"}"));
        hub.Update("target", DataHub.ParseValues("{\"v\":0,\"keep\":1}"));
        var result = await new CopyDisplayDataAction(hub).CreateExecutor().ExecuteAsync(Context(
            ("channel", "source"), ("destinationChannel", "target"), ("mode", mode)));
        Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Succeeded));
        Assert.That(hub.Read("target").Count, Is.EqualTo(count));
        Assert.That(hub.Read("target")["v"].GetInt32(), Is.EqualTo(42));
        hub.Update("source", DataHub.ParseValues("{\"v\":99}"));
        Assert.That(hub.Read("target")["v"].GetInt32(), Is.EqualTo(42));
    }

    [Test]
    public async Task MissingDataCannotEraseDestinationAndOversizeMergeIsAtomic()
    {
        var hub = new DataHub(); hub.Update("target", DataHub.ParseValues("{\"keep\":1}"));
        var action = new CopyDisplayDataAction(hub).CreateExecutor();
        var result = await action.ExecuteAsync(Context(("channel", "missing"), ("destinationChannel", "target"), ("mode", "replace")));
        Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Failed));
        Assert.That(hub.Read("target").ContainsKey("keep"), Is.True);
        hub.Update("source", Enumerable.Range(0, 64).ToDictionary(i => "v" + i, i => JsonSerializer.SerializeToElement(i)));
        result = await action.ExecuteAsync(Context(("channel", "source"), ("destinationChannel", "target")));
        Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Failed));
        Assert.That(hub.Read("target").Count, Is.EqualTo(1));
    }

    [Test]
    public async Task SameChannelAndDefaultDestinationNameWork()
    {
        var hub = new DataHub(); hub.Update("source", DataHub.ParseValues("{\"v\":42}"));
        var result = await new CopyDisplayValueAction(hub).CreateExecutor().ExecuteAsync(Context(
            ("channel", "source"), ("key", "v"), ("destinationChannel", "source")));
        Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Succeeded));
        Assert.That(hub.Read("source")["v"].GetInt32(), Is.EqualTo(42));
    }

    [TestCase("42.5", "42.5")][TestCase("true", "true")][TestCase("null", "")][TestCase("\"  日本語  \"", "  日本語  ")]
    public async Task VariableWriteUsesHostConversionAndOwnerScope(string json, string expected)
    {
        var hub = new DataHub(); hub.Update("source", DataHub.ParseValues("{\"v\":" + json + "}"));
        var api = new Variables();
        var action = new WriteDisplayVariableAction(hub, () => api).CreateExecutor();
        var result = await action.ExecuteAsync(Context(("channel", "source"), ("key", "v"), ("variable", "destination")));
        Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Succeeded));
        Assert.That(api.Last, Is.EqualTo(("destination", "owner", expected)));
        await action.ExecuteAsync(Context(("channel", "source"), ("key", "v"), ("variable", "destination"), ("scope", "global")));
        Assert.That(api.Last.Owner, Is.Null);
        result = await action.ExecuteAsync(Context(("channel", "source"), ("key", "missing"), ("variable", "destination")));
        Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Failed));
        Assert.That(api.Calls, Is.EqualTo(2));
    }

    [TestCase(UserVariableWriteStatus.NotFound, ActionErrorCodes.NotFound)]
    [TestCase(UserVariableWriteStatus.NotEditable, ActionErrorCodes.PermissionDenied)]
    [TestCase(UserVariableWriteStatus.Unavailable, ActionErrorCodes.NotConnected)]
    [TestCase(UserVariableWriteStatus.InvalidValue, ActionErrorCodes.InvalidParameter)]
    public async Task ReportsHostRejection(UserVariableWriteStatus status, string code)
    {
        var hub = new DataHub(); hub.Update("source", DataHub.ParseValues("{\"v\":42}"));
        var api = new Variables { Status = status };
        var result = await new WriteDisplayVariableAction(hub, () => api).CreateExecutor().ExecuteAsync(Context(
            ("channel", "source"), ("key", "v"), ("variable", "destination")));
        Assert.That(result.ErrorCode, Is.EqualTo(code));
    }

    [Test]
    public void CancelledCopyDoesNotWrite()
    {
        var hub = new DataHub(); hub.Update("source", DataHub.ParseValues("{\"v\":42}"));
        Assert.ThrowsAsync<OperationCanceledException>(async () => await new CopyDisplayDataAction(hub).CreateExecutor().ExecuteAsync(new()
        { Parameters = Context(("channel", "source"), ("destinationChannel", "target")).Parameters, CancellationToken = new(true) }));
        Assert.That(hub.Read("target"), Is.Empty);
    }

    private sealed class Variables : IUserVariableApi
    {
        public (string Name, string? Owner, string? Value) Last;
        public int Calls;
        public UserVariableWriteStatus Status = UserVariableWriteStatus.Applied;
        public Task<UserVariableWriteResult> ApplyAsync(string name, string? ownerWidgetId, UserVariableOperation operation, string? value, CancellationToken cancellationToken = default)
        {
            Assert.That(operation, Is.EqualTo(UserVariableOperation.Set));
            Last = (name, ownerWidgetId, value); Calls++;
            return Task.FromResult(new UserVariableWriteResult(Status, "Host detail"));
        }
    }
}
