using System.Text.Json;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class AnimationContinuityTests
{
    private sealed class Clock : TimeProvider
    {
        public long Time;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => Time;
    }
    private static double Frame(DisplayAnimation state, double target)
    {
        state.BeginFrame(); var value = state.Value("value", target, 1000, "linear"); state.EndFrame(); return value;
    }
    [Test] public void CheckpointPreservesElapsedTimeRetargetsAndIsIndependentAndSingleUse()
    {
        var clock = new Clock(); var cache = new AnimationContinuity(clock); var state = new DisplayAnimation(clock);
        Frame(state, 0); Frame(state, 100); clock.Time = 400;
        Assert.That(Frame(state, 100), Is.EqualTo(40)); cache.Save("one", state);
        state.Reset(); // The checkpoint must be a copy, not the disposed view's mutable state.
        clock.Time = 500; var restored = cache.Take("one")!;
        Assert.That(Frame(restored, 200), Is.EqualTo(50));
        clock.Time = 1000; Assert.That(Frame(restored, 200), Is.EqualTo(125));
        Assert.That(cache.Take("one"), Is.Null);
    }
    [Test] public void CheckpointsExpireAreBoundedAndCannotSurviveShutdown()
    {
        var clock = new Clock(); var cache = new AnimationContinuity(clock); var state = new DisplayAnimation(clock);
        cache.Save("expired", state); clock.Time = 2000; Assert.That(cache.Take("expired"), Is.Null);
        for (int i = 0; i < 65; i++) { clock.Time++; cache.Save(i.ToString(), state); }
        Assert.That(cache.Take("0"), Is.Null); Assert.That(cache.Take("64"), Is.Not.Null);
        cache.Stop(); cache.Save("late-dispose", state);
        Assert.That(cache.Take("1"), Is.Null); Assert.That(cache.Take("late-dispose"), Is.Null);
    }
    [Test] public void HsvCheckpointKeepsUnwrappedHueWhenTheTargetChanges()
    {
        var clock = new Clock(); var state = new DisplayAnimation(clock);
        string Color(DisplayAnimation animation, string target)
        {
            animation.BeginFrame(); var result = animation.Color("color", target, 1000, "linear", "hsv"); animation.EndFrame(); return result;
        }
        Color(state, "#0000ff"); Color(state, "#ff0000"); clock.Time = 400;
        Color(state, "#ff0000"); var copy = state.Copy();
        foreach (var target in new[] { "#ff0000", "#00ff00", "#0000ff" })
        {
            clock.Time += 100;
            Assert.That(Color(copy, target), Is.EqualTo(Color(state, target)));
        }
    }
    [Test] public void IdentitySeparatesWidgetsViewersDraftsAndChangedLayouts()
    {
        var surface = ButtonTests.Surface(); var settings = TestLayouts.Sample;
        var key = AnimationContinuity.Key(surface, settings);
        Assert.That(key, Is.Not.Null);
        Assert.That(AnimationContinuity.Key(ButtonTests.Surface("preview"), settings), Is.Not.EqualTo(key));
        Assert.That(AnimationContinuity.Key(surface, settings with { Layout = "<text id='other'>Other</text>" }), Is.Not.EqualTo(key));
        foreach (var attribute in new[] { "widgetId", "clientId", "viewerId" })
        {
            var attributes = surface.Attributes.ToDictionary(p => p.Key, p => p.Value);
            attributes[attribute] = JsonSerializer.SerializeToElement("another");
            Assert.That(AnimationContinuity.Key(surface with { Attributes = attributes }, settings), Is.Not.EqualTo(key));
        }
        foreach (var attribute in new[] { "sample", "ghost" })
        {
            var attributes = surface.Attributes.ToDictionary(p => p.Key, p => p.Value);
            attributes[attribute] = JsonSerializer.SerializeToElement(true);
            Assert.That(AnimationContinuity.Key(surface with { Attributes = attributes }, settings), Is.Null);
        }
    }
    [Test] public async Task RecreatedSessionContinuesColourInsteadOfStartingAtTarget()
    {
        var integration = new CustomButtonIntegration();
        var settings = new { channel = "demo", layout = "<stack id='content' background='{{color}}' transitionMs='10000' transitionProperties='background' easing='linear'/>", initialValues = "{\"color\":\"#ff0000\"}" };
        var attributes = ButtonTests.Surface().Attributes.ToDictionary(p => p.Key, p => p.Value);
        attributes["data"] = JsonSerializer.SerializeToElement(settings);
        var request = new MacroDeck.Sdk.Ui.UiSessionRequest { Surface = ButtonTests.Surface() with { Attributes = attributes }, UiModelVersion = 1 };
        var first = (ButtonSession)(await integration.CreateSessionAsync(request, default))!;
        integration.Hub.Update("demo", DataHub.ParseValues("{\"color\":\"#00ff00\"}")); first.Refresh();
        await Task.Delay(100); first.Refresh();
        await first.DisposeAsync();
        await using var second = (ButtonSession)(await integration.CreateSessionAsync(request, default))!;
        var tree = second.BuildTree();
        var color = ButtonTests.Nodes(tree.Root).Single(n => n.Id.EndsWith(".content")).Properties["background"].GetString();
        Assert.That(color, Is.Not.EqualTo("#00ff00"));
        Assert.That(color, Is.Not.EqualTo("#ff0000"));
        await integration.ShutdownAsync();
    }
}

