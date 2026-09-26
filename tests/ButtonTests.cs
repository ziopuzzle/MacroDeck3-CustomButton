using System.Text.Json;
using System.Xml;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Ui;
using MacroDeck.Plugin.Testing.Fakes;
using MacroDeck.Ui.Model.Events;
using MacroDeck.Ui.Model.Nodes;
using MacroDeck.Ui.Model.Surfaces;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class ButtonTests
{
    [TestCase("preview", false, false, 0)]
    [TestCase("preview", false, true, 0)]
    [TestCase("widget", true, false, 0)]
    [TestCase("widget", false, true, 0)]
    [TestCase("widget", false, false, 0)]
    public async Task LegacyScriptSettingsDoNotExecute(string kind, bool ghost, bool sample, int expected)
    {
        var integration = new CustomButtonIntegration();
        var context = new FakeIntegrationContext();
        context.Scripts.Seed(new() { Id = "update-id", Name = "Update" });
        await integration.InitializeAsync(context);
        var attrs = new Dictionary<string, JsonElement>(Surface(kind, sample: sample).Attributes);
        attrs["data"] = JsonSerializer.SerializeToElement(new { channel = "demo", layout = TestLayouts.SampleLayout, initialValues = "{}", updateScriptId = "update-id" });
        attrs["ghost"] = JsonSerializer.SerializeToElement(ghost);
        await using var session = await integration.CreateSessionAsync(new() { Surface = Surface(kind) with { Attributes = attrs }, UiModelVersion = 1 }, default);
        for (int i = 0; i < 30 && context.Scripts.Ran.Count < expected; i++) await Task.Delay(10);
        if (expected == 0) await Task.Delay(100);
        Assert.That(context.Scripts.Ran.Count, Is.EqualTo(expected));
        if (expected > 0) Assert.That(context.Scripts.Runs[0].OwnerWidgetId, Is.EqualTo("test-widget"));
    }

    [Test] public async Task UpdateScriptRunsImmediatelyRepeatsWithoutOverlapAndStopsOnDispose()
    {
        var hub = new DataHub();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repeated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int count = 0;
        var session = new ButtonSession(Surface(), TestLayouts.Sample, hub, update: async ct =>
        {
            int call = Interlocked.Increment(ref count);
            if (call == 1) { started.TrySetResult(); await release.Task.WaitAsync(ct); }
            hub.Update("demo", DataHub.ParseValues("{\"value\":88}"));
            if (call > 1) repeated.TrySetResult();
            return ActionResult.Success();
        });
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await Task.Delay(1100);
            Assert.That(count, Is.EqualTo(1));
            release.TrySetResult();
            await repeated.Task.WaitAsync(TimeSpan.FromSeconds(3));
            session.Refresh();
            Assert.That(Text(session.BuildTree(), ".value"), Is.EqualTo("88%"));
        }
        finally { await session.DisposeAsync(); }
        int stopped = count;
        await Task.Delay(1100);
        Assert.That(count, Is.EqualTo(stopped));
    }

    [Test] public async Task OmittedMainSizesRemainIntrinsicWhileExplicitSizesArePreserved()
    {
        await using var session = new ButtonSession(Surface(), TestLayouts.Sample, null);
        Assert.That(Nodes(session.BuildTree().Root).All(n => !n.Properties.ContainsKey("mainSize")), Is.True,
            "An explicit zero mainSize hides text in the host layout engine.");
        await using var fixedSize = new ButtonSession(Surface(), TestLayouts.Sample with
            { Layout = "<text id=\"fixed\" mainSize=\"0.25\">Visible</text>" }, null);
        Assert.That(Nodes(fixedSize.BuildTree().Root).Single(n => n.Id.EndsWith(".fixed")).Properties["mainSize"].GetProperty("basis").GetDouble(), Is.EqualTo(.25));
    }

    [Test] public async Task LegacyScriptSelectorsAreRemoved()
    {
        await using var config = new ConfigurationSession(Surface("config"), TestLayouts.Sample);
        Assert.That(Nodes(config.BuildTree().Root).Any(n => n.Id is "pressScriptId" or "updateScriptId"), Is.False);
        Assert.That(ButtonSettings.DefaultData, Does.Not.Contain("ScriptId"));
    }
    internal static UiSurface Surface(string kind = "widget", string type = CustomButtonIntegration.TypeId, bool sample = false) => new()
    {
        Kind = kind, SessionMode = kind == "config" ? "exclusive" : "shared",
        Attributes = new Dictionary<string, JsonElement>
        {
            ["widgetType"] = JsonSerializer.SerializeToElement(type), ["widgetId"] = JsonSerializer.SerializeToElement("test-widget"),
            ["data"] = JsonDocument.Parse(TestLayouts.SampleData).RootElement.Clone(),
            ["widgetData"] = JsonDocument.Parse(TestLayouts.SampleData).RootElement.Clone(),
            ["entryPoint"] = JsonSerializer.SerializeToElement("widget-config"), ["sample"] = JsonSerializer.SerializeToElement(sample)
        }
    };
    internal static IEnumerable<UiNode> Nodes(UiNode node) => new[] { node }.Concat(node.Children.SelectMany(Nodes));
    internal static string Text(UiTree tree, string suffix) => LocalizationTests.Resolve(Nodes(tree.Root).Single(n => n.Id.EndsWith(suffix, StringComparison.Ordinal)).Properties["text"]);
    private static ActionExecutionContext Context(params (string Key, object Value)[] parameters) => new() { Parameters = parameters.ToDictionary(p => p.Key, p => p.Value) };

    [Test] public async Task RegistersAConfigurableTypeWithDefaults()
    {
        var integration = new CustomButtonIntegration(); var context = new FakeWidgetTypeProviderContext();
        await integration.InitializeAsync(context);
        Assert.That(context.WidgetTypes["custom-button"].HasConfiguration, Is.True);
        Assert.That(context.WidgetTypes["custom-button"].DataSchema, Does.Contain("layout"));
    }
    [TestCase("widget")][TestCase("preview")][TestCase("config")]
    public async Task ServesEveryRequiredSurface(string kind)
    {
        await using var session = await new CustomButtonIntegration().CreateSessionAsync(new() { Surface = Surface(kind), UiModelVersion = 1 }, default);
        Assert.That(session, Is.Not.Null);
        Assert.That(session!.BuildTree().Surface.Kind, Is.EqualTo(kind));
        if (kind != "config")
        {
            Assert.That(Text(session.BuildTree(), ".value"), Is.EqualTo("42%"));
            Assert.That(Nodes(session.BuildTree().Root).All(n => n.Type.StartsWith("ui.")), Is.True);
        }
    }
    [Test] public async Task DeclinesOtherTypesAndUnknownSurfaces()
    {
        var integration = new CustomButtonIntegration();
        Assert.That(await integration.CreateSessionAsync(new() { Surface = Surface(type: "other::type"), UiModelVersion = 1 }, default), Is.Null);
        Assert.That(await integration.CreateSessionAsync(new() { Surface = Surface("folder"), UiModelVersion = 1 }, default), Is.Null);
    }
    [Test] public async Task ActionChangesTextAndBarOnAllMatchingButtonsOnly()
    {
        var hub = new DataHub();
        await using var first = new ButtonSession(Surface(), TestLayouts.Sample, hub);
        await using var second = new ButtonSession(Surface(), TestLayouts.Sample, hub);
        await using var other = new ButtonSession(Surface(), TestLayouts.Sample with { Channel = "other" }, hub);
        var result = await new UpdateValueAction(hub).CreateExecutor().ExecuteAsync(Context(("channel", "demo"), ("key", "value"), ("value", 75.5)));
        Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Succeeded));
        first.Refresh(); second.Refresh(); other.Refresh();
        Assert.That(Text(first.BuildTree(), ".value"), Is.EqualTo("75.5%"));
        Assert.That(Text(second.BuildTree(), ".value"), Is.EqualTo("75.5%"));
        Assert.That(Text(other.BuildTree(), ".value"), Is.EqualTo("42%"));
        Assert.That(Nodes(first.BuildTree().Root).Single(n => n.Id.EndsWith(".level")).Properties["end"].GetDouble(), Is.EqualTo(.755));
    }
    [Test] public async Task CoalescesPatchesAndMaintainsSnapshotRevision()
    {
        var hub = new DataHub(); await using var session = new ButtonSession(Surface(), TestLayouts.Sample, hub);
        for (int i = 1; i <= 20; i++) { hub.Update("demo", DataHub.ParseValues("{\"value\":" + i + "}")); session.Refresh(); }
        var patch = session.DrainPatches().Single();
        Assert.That(patch.FromRevision, Is.Zero);
        Assert.That(patch.ToRevision, Is.EqualTo(session.BuildTree().Revision));
        Assert.That(patch.Operations.All(op => op.Op == "set-properties"), Is.True);
        Assert.That(patch.Operations.Single(op => op.NodeId.EndsWith(".value")).Properties!["text"].GetString(), Is.EqualTo("20%"));
        Assert.That(Text(session.BuildTree(), ".value"), Is.EqualTo("20%"));
        session.Refresh(); Assert.That(session.DrainPatches(), Is.Empty);
        hub.Update("demo", DataHub.ParseValues("{\"value\":21}")); session.Refresh();
        Assert.That(session.DrainPatches().Single().FromRevision, Is.EqualTo(patch.ToRevision));
    }
    [Test] public async Task InvalidLayoutsProduceVisibleErrorsAndBadDataCanRecover()
    {
        await using var broken = new ButtonSession(Surface(), TestLayouts.Sample with { Layout = "<unknown id=\"x\"/>" }, null);
        Assert.That(Text(broken.BuildTree(), ".message"), Does.Contain("Unsupported"));
        var hub = new DataHub(); await using var live = new ButtonSession(Surface(), TestLayouts.Sample, hub);
        hub.Update("demo", DataHub.ParseValues("{\"value\":\"bad\"}")); live.Refresh();
        Assert.That(Text(live.BuildTree(), ".message"), Does.Contain("number"));
        hub.Update("demo", DataHub.ParseValues("{\"value\":10}")); live.Refresh();
        Assert.That(Text(live.BuildTree(), ".value"), Is.EqualTo("10%"));
    }
    [Test] public void TreatsInsertedMarkupAsTextAndFormatsNumbers()
    {
        var data = DataHub.ParseValues("{\"value\":42.125,\"title\":\"日本語 <tag> & \\\"quote\\\" {{value}}\"}");
        Assert.That(LayoutRenderer.Expand("{{title}}", data), Is.EqualTo("日本語 <tag> & \"quote\" {{value}}"));
        Assert.That(LayoutRenderer.Expand("{{value:0.0}} / {{missing}}", data), Is.EqualTo("42.1 / —"));
    }
    [TestCase("<stack id=\"root\"><text id=\"same\"/><text id=\"same\"/></stack>")]
    [TestCase("<text id=\"x\" onclick=\"bad\">Text</text>")]
    [TestCase("<text>Missing id</text>")]
    public void RejectsMalformedLayouts(string xml) => Assert.Throws<FormatException>(() => new LayoutRenderer(xml));
    [Test] public void ProhibitsDtd() => Assert.Throws<XmlException>(() => new LayoutRenderer("<!DOCTYPE text [<!ENTITY x SYSTEM 'file:///secret'>]><text id=\"a\">&x;</text>"));
    [TestCase("[]")][TestCase("{\"x\":[]}")][TestCase("{\"x\":1,\"x\":2}")]
    public void RejectsAmbiguousData(string json) => Assert.Throws<FormatException>(() => DataHub.ParseValues(json));
    [Test] public async Task FailedUpdateLeavesLastGoodValue()
    {
        var hub = new DataHub(); hub.Update("demo", DataHub.ParseValues("{\"value\":12}"));
        var result = await new UpdateValuesAction(hub).CreateExecutor().ExecuteAsync(Context(("channel", "demo"), ("json", "[]")));
        Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Failed));
        Assert.That(hub.Read("demo")["value"].GetInt32(), Is.EqualTo(12));
    }
    [Test] public async Task PickerSampleNeverReadsLiveValues()
    {
        var integration = new CustomButtonIntegration(); integration.Hub.Update("demo", DataHub.ParseValues("{\"value\":99}"));
        await using var sample = await integration.CreateSessionAsync(new() { Surface = Surface("preview", sample: true), UiModelVersion = 1 }, default);
        Assert.That(Nodes(sample!.BuildTree().Root).Any(n => n.Type == "ui.text" || n.Type == "ui.range-bar"), Is.False);
        Assert.That(Nodes(sample.BuildTree().Root).Any(n => n.Id.EndsWith(".container")), Is.True);
    }
    [Test] public async Task ConfigInputUsesStoredDataKeysAndProducesEdits()
    {
        await using var config = new ConfigurationSession(Surface("config"), TestLayouts.Sample);
        var keys = Nodes(config.BuildTree().Root).Select(n => n.Id).ToArray();
        Assert.That(keys, Does.Contain("layout")); Assert.That(keys, Does.Contain("initialValues"));
        config.Dispatch(new UiEvent { NodeId = "channel", Name = "change", Data = JsonSerializer.SerializeToElement("new-channel") });
        Assert.That(config.DrainPatches(), Is.Not.Empty);
    }
    [Test] public async Task PressDispatchIsValidatedAndDisposalCancelsWork()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int count = 0;
        var session = new ButtonSession(Surface(), TestLayouts.Sample, null, async ct =>
        {
            Interlocked.Increment(ref count); started.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, ct); } catch (OperationCanceledException) { cancelled.TrySetResult(); throw; }
            return ActionResult.Success();
        });
        session.Dispatch(new UiEvent { NodeId = "other", Name = "press" }); Assert.That(count, Is.Zero);
        session.Dispatch(new UiEvent { NodeId = "root", Name = "press" }); await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        session.Dispatch(new UiEvent { NodeId = "root", Name = "press" }); Assert.That(count, Is.EqualTo(1));
        await session.DisposeAsync(); await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(3));
        session.Refresh(); Assert.That(session.DrainPatches(), Is.Empty);
    }
}


