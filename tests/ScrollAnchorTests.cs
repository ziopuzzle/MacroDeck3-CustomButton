using System.Text.Json;
using MacroDeck.Ui.Runtime;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class ScrollAnchorTests
{
    [TestCase(0)]
    [TestCase(1)]
    public void ShortDelayStillRequiresSeparateDeliveries(int delay)
    {
        double clock = 0;
        var renderer = new LayoutRenderer($"<scroll id='s' followKey='target' followDelayMs='{delay}'><repeat id='r' items='rows'><text id='t'>{{item}}</text></repeat></scroll>")
        { ScrollClock = () => clock, RequireScrollDelivery = true };
        var data = DataHub.ParseValues("""{"target":0,"rows":[1,2]}""");
        int Count() => ButtonTests.Nodes(new UiView(ButtonTests.Surface(), renderer.Render(data)).Tree.Root).Count(n => n.Type == "ui.text");
        Assert.That(Count(), Is.Zero);
        clock += 100;
        Assert.That(Count(), Is.Zero);
        renderer.ScrollDelivered(); clock += delay;
        Assert.That(Count(), Is.EqualTo(1));
        Assert.That(Count(), Is.EqualTo(1));
        renderer.ScrollDelivered(); clock += delay;
        Assert.That(Count(), Is.EqualTo(2));
    }

    [Test]
    public void FollowStagesWaitForDeliveryAndStopAfterRestoringRows()
    {
        double clock = 0;
        var renderer = new LayoutRenderer("<scroll id='s' followKey='target' followOffset='1'><repeat id='r' items='rows'><text id='t'>{{item}}</text></repeat></scroll>")
        { ScrollClock = () => clock, RequireScrollDelivery = true };
        var values = DataHub.ParseValues("{\"target\":4,\"rows\":[1,2,3,4,5,6,7,8]}");
        MacroDeck.Ui.Model.Nodes.UiNode Render(int count, string anchor)
        {
            var root = new UiView(ButtonTests.Surface(), renderer.Render(values)).Tree.Root;
            Assert.That(root.Properties["anchor"].GetString(), Is.EqualTo(anchor));
            Assert.That(ButtonTests.Nodes(root).Count(n => n.Type == "ui.text"), Is.EqualTo(count));
            return root;
        }
        var empty = Render(0, "end");
        clock = 1000; Render(0, "end"); // Undelivered snapshots cannot skip phases.
        renderer.ScrollDelivered();
        clock += 299; Render(0, "end");
        clock++; var prefix = Render(6, "end");
        renderer.ScrollDelivered(); clock += 300;
        var full = Render(8, "start");
        Assert.That(renderer.ScrollFollowActive, Is.False);
        Assert.That(TreeChanges.Between(prefix, full).Any(op => op.Op == "replace-node" && op.NodeId == full.Id), Is.False);
        clock += 10000; Render(8, "start");
        values["target"] = JsonSerializer.SerializeToElement(1);
        Render(0, "end"); renderer.ScrollDelivered(); clock += 300;
        Render(3, "end");
        values["target"] = JsonSerializer.SerializeToElement(-1);
        Render(8, "start"); Assert.That(renderer.ScrollFollowActive, Is.False);
    }

    [Test]
    public void FollowRejectsUnsupportedStructure()
    {
        foreach (var xml in new[] {
            "<scroll id='s' followKey='x'><text id='t'>Hi</text></scroll>",
            "<scroll id='s' followKey='x' direction='horizontal'><repeat id='r' items='rows'/></scroll>" })
            Assert.Throws<FormatException>(() => new LayoutRenderer(xml).Render(DataHub.ParseValues("{}")));
    }

    [Test]
    public void AnchorToggleIsAPropertyPatchNotAListReplacement()
    {
        var renderer = new LayoutRenderer("<scroll id='page' anchor='{{anchor}}'><text id='label'>Text</text></scroll>");
        var start = new UiView(ButtonTests.Surface(), renderer.Render(DataHub.ParseValues("{\"anchor\":\"start\"}"))).Tree.Root;
        var end = new UiView(ButtonTests.Surface(), renderer.Render(DataHub.ParseValues("{\"anchor\":\"end\"}"))).Tree.Root;
        Assert.That(start.RequiredComponentVersion, Is.EqualTo(3));
        Assert.That(start.Fallback, Is.Not.Null);
        foreach (var pair in new[] { (start, end), (end, start) })
        {
            var patches = TreeChanges.Between(pair.Item1, pair.Item2);
            Assert.That(patches.Single().Op, Is.EqualTo("set-properties"));
            Assert.That(patches.Single().Properties!["anchor"].GetString(), Is.EqualTo(pair.Item2.Properties["anchor"].GetString()));
        }
    }

    [Test]
    public void ProbeKeepsViewportAliveAcrossAllFivePhases()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "experiments/scroll-anchor"))) directory = directory.Parent;
        var path = Path.Combine(directory!.FullName, "experiments/scroll-anchor");
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(path, "widget.json")));
        var settings = ButtonSettings.Read(config.RootElement);
        Assert.That(settings.Layout, Is.EqualTo(File.ReadAllText(Path.Combine(path, "layout.xml"))));
        var flows = ButtonEvents.Normalize(settings.Flows, "test-widget");
        Assert.That(flows[0].GetProperty("children").GetArrayLength(), Is.EqualTo(9));
        var renderer = new LayoutRenderer(settings.Layout);
        var values = DataHub.ParseValues(settings.InitialValues);
        MacroDeck.Ui.Model.Nodes.UiNode? previous = null;
        var counts = new[] { 8, 5, 6, 6, 8 };
        for (var phase = 0; phase <= 4; phase++)
        {
            values["phase"] = JsonSerializer.SerializeToElement(phase);
            var root = new UiView(ButtonTests.Surface(), renderer.Render(values)).Tree.Root;
            LayoutLimits.Validate(root);
            var list = ButtonTests.Nodes(root).Single(n => n.Type == "ui.list");
            Assert.That(list.Properties["anchor"].GetString(), Is.EqualTo(phase is 1 or 2 ? "end" : "start"));
            Assert.That(ButtonTests.Nodes(list).Count(n => n.Type == "ui.text" && n.Properties["text"].GetString()!.StartsWith("Row ")), Is.EqualTo(counts[phase]));
            if (previous != null)
                Assert.That(TreeChanges.Between(previous, root).Any(op => op.Op == "replace-node" && (op.NodeId == root.Id || op.NodeId == list.Id)), Is.False, $"Phase {phase} must keep the native scroll position.");
            previous = root;
        }
    }

    [Test]
    public void OmittedAnchorKeepsExistingCompatibilityAndInvalidSettingsFail()
    {
        var root = new UiView(ButtonTests.Surface(), new LayoutRenderer("<scroll id='page'/>").Render(DataHub.ParseValues("{}"))).Tree.Root;
        Assert.That(root.Properties.ContainsKey("anchor"), Is.False);
        foreach (var attributes in new[] { "anchor='middle'", "anchor='end' direction='horizontal'" })
            Assert.Throws<FormatException>(() => new LayoutRenderer($"<scroll id='page' {attributes}/>").Render(DataHub.ParseValues("{}")));
    }
}
