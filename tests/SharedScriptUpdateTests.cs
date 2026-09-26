using System.Text.Json;
using MacroDeck.Sdk.Actions;
using MacroDeck.Plugin.Testing.Fakes;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class SharedScriptUpdateTests
{
    [Test] public async Task LegacyScriptIsIgnoredInPreviewWithVariableScope()
    {
        var integration = new CustomButtonIntegration(); var context = new FakeIntegrationContext();
        context.Scripts.Seed(new() { Id = "update", Name = "Update" });
        await integration.InitializeAsync(context);
        var attributes = new Dictionary<string, JsonElement>(ButtonTests.Surface("preview").Attributes);
        attributes.Remove("widgetId"); attributes["variableScopeWidgetId"] = JsonSerializer.SerializeToElement("edited-widget");
        attributes["data"] = JsonSerializer.SerializeToElement(new { channel = "demo", layout = TestLayouts.SampleLayout, initialValues = "{}", updateScriptId = "update" });
        await using var preview = (await integration.CreateSessionAsync(new() { Surface = ButtonTests.Surface("preview") with { Attributes = attributes }, UiModelVersion = 1 }, default))!;
        await Task.Delay(1100);
        Assert.That(context.Scripts.Runs, Is.Empty);
    }

    [Test] public async Task PreviewAndLiveWidgetShareOneRunnerAndContinueWhenEitherCloses()
    {
        var updates = new SharedScriptUpdates(); var hub = new DataHub(); var count = 0;
        Task<ActionResult> Run(CancellationToken ct)
        {
            hub.Update("demo", DataHub.ParseValues($"{{\"value\":{Interlocked.Increment(ref count)}}}"));
            return Task.FromResult(ActionResult.Success());
        }
        var preview = new ButtonSession(ButtonTests.Surface("preview"), TestLayouts.Sample, hub,
            startSharedUpdates: () => updates.Acquire(new("widget", "script"), Run));
        var live = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample, hub,
            startSharedUpdates: () => updates.Acquire(new("widget", "script"), Run));
        try
        {
            await Until(() => ButtonTests.Text(preview.BuildTree(), ".value") == "1%" && ButtonTests.Text(live.BuildTree(), ".value") == "1%");
            Assert.That(count, Is.EqualTo(1));
            await live.DisposeAsync();
            await Until(() => ButtonTests.Text(preview.BuildTree(), ".value") == "2%");
        }
        finally { await preview.DisposeAsync(); await live.DisposeAsync(); }
        var stopped = count; await Task.Delay(1100); Assert.That(count, Is.EqualTo(stopped));
    }

    [Test] public async Task DifferentWidgetScopesAndScriptsDoNotShareARunner()
    {
        var updates = new SharedScriptUpdates(); var count = 0;
        Task<ActionResult> Run(CancellationToken ct) { Interlocked.Increment(ref count); return Task.FromResult(ActionResult.Success()); }
        await using var a = updates.Acquire(new("a", "one"), Run);
        await using var b = updates.Acquire(new("b", "one"), Run);
        await using var c = updates.Acquire(new("a", "two"), Run);
        await Until(() => count == 3);
    }

    [Test] public async Task ReopeningWaitsForThePreviousInFlightScriptToStop()
    {
        var updates = new SharedScriptUpdates(); var count = 0;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = updates.Acquire(new("a", "one"), async ct =>
        {
            Interlocked.Increment(ref count); started.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, ct); } catch (OperationCanceledException) { canceled.TrySetResult(); }
            await release.Task; return ActionResult.Success();
        });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var closing = old.DisposeAsync().AsTask();
        await canceled.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await using var next = updates.Acquire(new("a", "one"), ct => { Interlocked.Increment(ref count); return Task.FromResult(ActionResult.Success()); });
        try { await Task.Delay(100); Assert.That(count, Is.EqualTo(1)); }
        finally { release.TrySetResult(); await closing; }
        await Until(() => count == 2);
    }

    [Test] public async Task SharedErrorsAppearInPreviewAndClearAfterRecovery()
    {
        var updates = new SharedScriptUpdates(); var count = 0;
        await using var preview = new ButtonSession(ButtonTests.Surface("preview"), TestLayouts.Sample, null,
            startSharedUpdates: () => updates.Acquire(new("a", "one"), ct =>
            {
                if (Interlocked.Increment(ref count) == 1) throw new InvalidOperationException("test");
                return Task.FromResult(ActionResult.Success());
            }));
        await Until(() => ButtonTests.Nodes(preview.BuildTree().Root).Any(n => n.Id.EndsWith(".message")));
        await Until(() => ButtonTests.Nodes(preview.BuildTree().Root).Any(n => n.Id.EndsWith(".value")));
        Assert.That(ButtonTests.Text(preview.BuildTree(), ".value"), Is.EqualTo("42%"));
    }

    private static async Task Until(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(20, timeout.Token);
    }
}

