using MacroDeck.Ui.Components;
using MacroDeck.Ui.Runtime;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class FontFaceTests
{
    [TestCase("")][TestCase(" fontFace='' ")][TestCase(" fontFace='{{missing}}' ")]
    public void UnspecifiedFontsKeepTheHostDefault(string attribute)
    {
        var root = new UiView(ButtonTests.Surface(), new LayoutRenderer($"<text id='title'{attribute}>Title</text>").Render(DataHub.ParseValues("{}"))).Tree.Root;
        Assert.That(root.Properties.ContainsKey("fontFace"), Is.False);
    }

    [Test] public async Task ConditionalFontChangesPatchOnlyTheTextAndCanReturnToDefault()
    {
        var hub = new DataHub();
        var settings = new ButtonSettings("demo", """
            <stack id="panel">
              <text id="title" fontFace="{{face}}" color="#ffffff80" maxLines="2">Title<style when="reset == 1" fontFace=""/></text>
              <text id="other" fontFace="other-face">Other</text>
            </stack>
            """, "{\"face\":\"first-face\",\"reset\":0}");
        await using var session = new ButtonSession(ButtonTests.Surface(), settings, hub);
        var initial = ButtonTests.Nodes(session.BuildTree().Root).Where(n => n.Type == "ui.text").ToArray();
        Assert.That(initial.Select(n => n.Properties["fontFace"].GetString()), Is.EqualTo(new[] { "first-face", "other-face" }));
        hub.Update("demo", DataHub.ParseValues("{\"face\":\"second-face\"}")); session.Refresh();
        var change = session.DrainPatches().Single().Operations.Single();
        Assert.That(change.Op, Is.EqualTo("set-properties"));
        Assert.That(change.Properties!["fontFace"].GetString(), Is.EqualTo("second-face"));
        hub.Update("demo", DataHub.ParseValues("{\"reset\":1}")); session.Refresh();
        var reset = session.DrainPatches().Single().Operations.Single();
        Assert.That(reset.RemovedProperties, Does.Contain("fontFace"));
    }
}

