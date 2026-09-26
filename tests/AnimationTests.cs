using MacroDeck.Ui.Runtime;
using MacroDeck.Ui.Model.Nodes;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class AnimationTests
{
    [Test] public void NeedleKeepsLengthDuringAngularTransition()
    {
        var clock = new Clock(); var state = new DisplayAnimation(clock);
        var renderer = new LayoutRenderer("<line id='needle' coordinates='local' cx='50%' cy='50%' length='40%' angle='{{angle}}' transitionMs='1000' transitionProperties='angle' easing='linear'/>");
        string Render(int angle) => ButtonTests.Nodes(new UiView(ButtonTests.Surface(), renderer.Render(DataHub.ParseValues("{\"angle\":" + angle + "}"), animation: state)).Tree.Root)
            .Single(n => n.Type == "ui.shape").Properties["path"].GetString()!;
        Render(-180); Render(0); clock.Milliseconds = 500;
        var path = Render(0);
        var coordinates = path.Split('L')[1].Split(' ').Select(s => double.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        Assert.That(coordinates[0], Is.EqualTo(.5).Within(1e-10));
        Assert.That(coordinates[1], Is.EqualTo(.1).Within(1e-10));
        Assert.That(LayoutOptions.TransitionProperties("line"), Does.Contain("angle"));
    }
    private sealed class Clock : TimeProvider
    {
        public long Milliseconds;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => Milliseconds;
    }
    [TestCase("linear", .25)][TestCase("ease-in", .0625)]
    [TestCase("ease-out", .4375)][TestCase("ease-in-out", .15625)]
    public void EasingUsesElapsedTime(string easing, double expected)
    {
        var clock = new Clock(); var state = new DisplayAnimation(clock);
        double Sample(double target) { state.BeginFrame(); var v = state.Value("a", target, 1000, easing); state.EndFrame(); return v; }
        Assert.That(Sample(0), Is.Zero);
        Assert.That(Sample(1), Is.Zero);
        clock.Milliseconds = 250;
        Assert.That(Sample(1), Is.EqualTo(expected));
        Assert.That(state.IsActive, Is.True);
        clock.Milliseconds = 1000;
        Assert.That(Sample(1), Is.EqualTo(1));
        Assert.That(state.IsActive, Is.False);
    }
    [Test] public void RetargetStartsAtCurrentPositionAndRemovedTracksDoNotLeak()
    {
        var clock = new Clock(); var state = new DisplayAnimation(clock);
        double Sample(double target) { state.BeginFrame(); var v = state.Value("a", target, 1000, "linear"); state.EndFrame(); return v; }
        Sample(0); Sample(100);
        clock.Milliseconds = 500;
        Assert.That(Sample(20), Is.EqualTo(50));
        clock.Milliseconds = 1000;
        Assert.That(Sample(20), Is.EqualTo(35));
        state.BeginFrame(); state.EndFrame();
        Assert.That(state.IsActive, Is.False);
        Assert.That(Sample(80), Is.EqualTo(80));
    }
    [Test] public void OpacityAndColourProduceIntermediatePropertiesWithoutChangingNodeIds()
    {
        var clock = new Clock(); var state = new DisplayAnimation(clock);
        var renderer = new LayoutRenderer("<text id='label' transitionMs='1000' transitionProperties='opacity color' easing='linear' color='#000000'><style when='on == 1' opacity='0' color='#ffffff'/>Hello</text>");
        UiNode Render(int on) => new UiView(ButtonTests.Surface(), renderer.Render(DataHub.ParseValues("{\"on\":" + on + "}"), animation: state)).Tree.Root;
        var initial = Render(0); Render(1); clock.Milliseconds = 500;
        var middle = Render(1);
        Assert.That(middle.Properties["opacity"].GetDouble(), Is.EqualTo(.5));
        Assert.That(ButtonTests.Nodes(middle).Single(n => n.Type == "ui.text").Properties["color"].GetString(), Is.EqualTo("#bcbcbc"));
        Assert.That(ButtonTests.Nodes(middle).Select(n => n.Id), Is.EqualTo(ButtonTests.Nodes(initial).Select(n => n.Id)));
        clock.Milliseconds = 1000;
        var final = Render(1);
        Assert.That(final.Properties["opacity"].GetDouble(), Is.Zero);
        Assert.That(state.IsActive, Is.False);
        Assert.That(TreeChanges.Between(initial, final).All(p => p.Op == "set-properties"), Is.True);
    }
    [Test] public async Task SessionContinuesAfterSingleUpdateThenStopsWithoutChangingStoredData()
    {
        var hub = new DataHub();
        var settings = new ButtonSettings("demo", "<bar id='level' value='{{value}}' min='0' max='100' transitionMs='180' transitionProperties='value' easing='linear'/>", "{\"value\":0}");
        await using var session = new ButtonSession(ButtonTests.Surface(), settings, hub);
        double Value() => ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id.EndsWith(".level")).Properties["end"].GetDouble();
        hub.Update("demo", DataHub.ParseValues("{\"value\":100}")); session.Refresh();
        Assert.That(Value(), Is.LessThan(1));
        for (int i = 0; i < 200 && Value() < 1; i++) await Task.Delay(10);
        Assert.That(Value(), Is.EqualTo(1));
        Assert.That(hub.Read("demo")["value"].GetInt32(), Is.EqualTo(100));
        Assert.That(hub.ReadHistory("demo", "value", 120), Is.EqualTo(new[] { 100d }));
        var revision = session.BuildTree().Revision;
        await Task.Delay(150);
        Assert.That(session.BuildTree().Revision, Is.EqualTo(revision));
    }
    [Test] public void LengthUnitsNormalizeAndHiddenElementsRestartImmediately()
    {
        var clock = new Clock(); var state = new DisplayAnimation(clock);
        var renderer = new LayoutRenderer("<line id='line' length='10%' visibleWhen='show == 1' transitionMs='1000' transitionProperties='length' easing='linear'><style when='wide == 1' length='0.9'/></line>");
        UiNode Render(int wide, int show = 1) => new UiView(ButtonTests.Surface(), renderer.Render(DataHub.ParseValues($"{{\"wide\":{wide},\"show\":{show}}}"), animation: state)).Tree.Root;
        Render(0); Render(1); clock.Milliseconds = 500;
        var middle = Render(1);
        Assert.That(ButtonTests.Nodes(middle).Single(n => n.Id.EndsWith(".row.body")).Properties["mainSize"].GetProperty("basis").GetDouble(), Is.EqualTo(.5));
        Render(1, 0); Assert.That(state.IsActive, Is.False);
        var shown = Render(1);
        Assert.That(ButtonTests.Nodes(shown).Single(n => n.Id.EndsWith(".row.body")).Properties["mainSize"].GetProperty("basis").GetDouble(), Is.EqualTo(.9));
    }
    [Test] public async Task FadingInputStaysUsableAndClosingViewStopsFrames()
    {
        var hub = new DataHub(); var calls = new List<ControlInput>();
        var settings = new ButtonSettings("demo", "<stack id='target' interactive='true' opacity='{{opacity}}' transitionMs='1000'><text id='label'>Press</text></stack>", "{\"opacity\":1}");
        var session = new ButtonSession(ButtonTests.Surface(), settings, hub, onControlInput: calls.Add);
        var initial = session.BuildTree();
        var target = ButtonTests.Nodes(initial.Root).Single(n => n.Id.EndsWith(".target")).Id;
        try
        {
            hub.Update("demo", DataHub.ParseValues("{\"opacity\":0.2}")); session.Refresh();
            await Task.Delay(80);
            session.Dispatch(new() { NodeId = target, Name = "press", Revision = initial.Revision });
            Assert.That(calls, Has.Count.EqualTo(1));
        }
        finally { await session.DisposeAsync(); }
        var revision = session.BuildTree().Revision;
        await Task.Delay(80);
        Assert.That(session.BuildTree().Revision, Is.EqualTo(revision));
    }
    [TestCase("transitionMs='-1'")][TestCase("transitionMs='10001'")]
    [TestCase("transitionMs='NaN'")][TestCase("transitionProperties='text'")][TestCase("easing='unknown'")]
    public void InvalidOptionsAreExplained(string attributes)
        => Assert.Throws<FormatException>(() => new LayoutRenderer($"<text id='t' {attributes}>A</text>").Render(DataHub.ParseValues("{}")));
}

