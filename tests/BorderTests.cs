using MacroDeck.Sdk.Actions;
using MacroDeck.Ui.Model.Events;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class BorderTests
{
    [TestCase("static")][TestCase("heartbeat")][TestCase("breathing")][TestCase("blink")]
    [TestCase("comet")][TestCase("ants")][TestCase("hue-shift")][TestCase("rgb")][TestCase("none")]
    public async Task BorderStaysBelowRootToAvoidHostSuppressionAndStillRunsScript(string style)
    {
        var calls = 0;
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with
        { Layout = $"<stack id=\"face\" borderStyle=\"{style}\" borderColor=\"#abc\"><text id=\"label\">Hello</text></stack>" }, null,
            press: _ => { calls++; return Task.FromResult(ActionResult.Success()); });
        var root = session.BuildTree().Root;
        Assert.That(root.Id, Is.EqualTo("root"));
        Assert.That(root.Properties.ContainsKey("borderStyle"), Is.False);
        var frame = root.Children.Single();
        Assert.That(frame.Id, Does.EndWith(".face"));
        if (style != "none") Assert.That(frame.Properties["corner"].GetString(), Is.EqualTo("tile"));
        if (style != "none") Assert.That(frame.Properties["borderColor"].GetString(), Is.EqualTo("#aabbcc"));
        Assert.That(frame.Properties.ContainsKey("borderStyle"), Is.EqualTo(style != "none"));
        if (style != "none") Assert.That(frame.Properties["borderStyle"].GetString(), Is.EqualTo(style));
        Assert.That(frame.Children.Single().Properties["text"].GetString(), Is.EqualTo("Hello"));
        session.Dispatch(new UiEvent { NodeId = root.Id, Name = "press" });
        for (var i = 0; i < 100 && calls == 0; i++) await Task.Delay(10);
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test] public async Task BorderConditionsToggleOffAndOnWithoutChangingRootIdentity()
    {
        var hub = new DataHub();
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with
        { Layout = "<stack id=\"face\" borderStyle=\"none\"><style when=\"value >= 80\" borderStyle=\"blink\" borderColor=\"#f00\"/><text id=\"label\">{{value}}</text></stack>" }, hub);
        Assert.That(session.BuildTree().Root.Children.Single().Properties.ContainsKey("borderStyle"), Is.False);
        hub.Update("demo", DataHub.ParseValues("{\"value\":85}")); session.Refresh();
        Assert.That(session.BuildTree().Root.Id, Is.EqualTo("root"));
        Assert.That(session.BuildTree().Root.Properties.ContainsKey("borderStyle"), Is.False);
        Assert.That(session.BuildTree().Root.Children.Single().Properties["borderStyle"].GetString(), Is.EqualTo("blink"));
        Assert.That(session.BuildTree().Root.Children.Single().Properties["borderColor"].GetString(), Is.EqualTo("#ff0000"));
        hub.Update("demo", DataHub.ParseValues("{\"value\":20}")); session.Refresh();
        Assert.That(session.BuildTree().Root.Children.Single().Properties.ContainsKey("borderStyle"), Is.False);
    }
}


