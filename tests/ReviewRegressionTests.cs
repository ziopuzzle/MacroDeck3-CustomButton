using System.Text.Json;
using MacroDeck.Sdk.Actions;
using MacroDeck.Ui.Model.Events;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class ReviewRegressionTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Test] public void SnapshotKeepsValueAndHistoryTogetherAndIsReusedUntilTheNextUpdate()
    {
        var clock = new Clock(); var hub = new DataHub(clock);
        hub.Update("demo", DataHub.ParseValues("{\"value\":12}"));
        var before = hub.Snapshot("demo");
        Assert.That(hub.Snapshot("demo"), Is.SameAs(before));
        clock.Now = clock.Now.AddSeconds(1);
        hub.Update("demo", DataHub.ParseValues("{\"value\":20}"));
        var after = hub.Snapshot("demo");
        Assert.That(after.Revision, Is.GreaterThan(before.Revision));
        Assert.That(before.Values["value"].GetInt32(), Is.EqualTo(12));
        Assert.That(before.ReadHistory("value", 60), Is.EqualTo(new[] {12d}));
        Assert.That(after.Values["value"].GetInt32(), Is.EqualTo(20));
        Assert.That(after.ReadHistory("value", 60), Is.EqualTo(new[] {12d, 20d}));
        Assert.Throws<FormatException>(() => hub.Update("demo", DataHub.ParseValues("{\"invalid\":[]}")));
        Assert.That(hub.Snapshot("demo"), Is.SameAs(after));
    }

    [Test] public async Task IdleRefreshAvoidsRebuildingWhileRepeatedReadingsStillAdvanceHistory()
    {
        var clock = new Clock(); var hub = new DataHub(clock);
        hub.Update("demo", DataHub.ParseValues("{\"chart_value\":42}"));
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with { Layout = LayoutTemplates.Get("widget_history-chart").Xml }, hub);
        session.Refresh();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) session.Refresh();
        Assert.That(GC.GetAllocatedBytesForCurrentThread() - allocated, Is.LessThan(65536), "Idle checks should not allocate UI trees or serialize them.");
        Assert.That(session.DrainPatches(), Is.Empty);
        clock.Now = clock.Now.AddSeconds(1);
        hub.Update("demo", DataHub.ParseValues("{\"chart_value\":42}")); session.Refresh();
        Assert.That(session.DrainPatches(), Is.Not.Empty, "Equal values in a new second still add a chart point.");
    }

    [TestCase("true", "flag == true", true)]
    [TestCase("False", "flag == false", true)]
    [TestCase("false", "true != flag", true)]
    [TestCase("yes", "flag == true", false)]
    [TestCase("001", "flag == '001'", true)]
    public async Task TextActionBooleanValuesWorkWithConditionalDisplay(string value, string expression, bool expected)
    {
        var hub = new DataHub();
        var result = await new UpdateValueAction(hub).CreateExecutor().ExecuteAsync(new ActionExecutionContext {
            Parameters = new Dictionary<string, object> { ["channel"] = "demo", ["key"] = "flag", ["value"] = value }
        });
        Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Succeeded));
        Assert.That(hub.Read("demo")["flag"].GetString(), Is.EqualTo(value));
        Assert.That(DisplayCondition.Evaluate(expression, hub.Read("demo")), Is.EqualTo(expected));
    }

    [Test] public async Task ScriptErrorAndRecoveryRefreshEvenWithoutADataUpdate()
    {
        var attempts = 0;
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample, null,
            press: _ => Task.FromResult(Interlocked.Increment(ref attempts) == 1 ? ActionResult.Failed(ActionErrorCodes.InvalidParameter, "Test") : ActionResult.Success()));
        session.Dispatch(new UiEvent { NodeId = "root", Name = "press" });
        for (var i = 0; i < 100 && !ButtonTests.Nodes(session.BuildTree().Root).Any(n => n.Id.EndsWith(".message")); i++) { await Task.Delay(10); session.Refresh(); }
        Assert.That(ButtonTests.Text(session.BuildTree(), ".message"), Does.Contain("Script execution failed"));
        session.Dispatch(new UiEvent { NodeId = "root", Name = "press" });
        for (var i = 0; i < 100 && ButtonTests.Nodes(session.BuildTree().Root).Any(n => n.Id.EndsWith(".message")); i++) { await Task.Delay(10); session.Refresh(); }
        Assert.That(ButtonTests.Text(session.BuildTree(), ".value"), Is.EqualTo("42%"));
    }

    [Test] public async Task OldTemplateMetadataDoesNotOverrideXmlAndDisposedConfigIgnoresInput()
    {
        var data = JsonSerializer.SerializeToElement(new { templateId = "widget_history-chart", layout = "<text id='saved'>Saved</text>", designPreset = "xml" });
        await using var session = new ButtonSession(ButtonTests.Surface(), ButtonSettings.Read(data), null);
        Assert.That(ButtonTests.Text(session.BuildTree(), ".saved"), Is.EqualTo("Saved"));
        var config = new ConfigurationSession(ButtonTests.Surface("config"), TestLayouts.Sample);
        await config.DisposeAsync();
        config.Dispatch(new UiEvent { NodeId = "channel", Name = "change", Data = JsonSerializer.SerializeToElement("changed") });
        Assert.That(ButtonTests.Nodes(config.BuildTree().Root).Single(n => n.Id == "channel").Properties["value"].GetString(), Is.EqualTo("demo"));
    }
}



