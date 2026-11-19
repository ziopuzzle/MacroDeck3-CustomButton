using System.Text.Json;
using MacroDeck.Ui.Model.Events;
using MacroDeck.Ui.Runtime;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class RendererBehaviorTests
{
    [Test]
    public void HiddenSubtreesDeferPaintValidationButStillValidateTransitions()
    {
        var values = DataHub.ParseValues("{}");
        var hidden = new LayoutRenderer("<stack id='hidden' visible='false' background='invalid'><text id='child' color='invalid'>Hidden</text></stack>");
        Assert.DoesNotThrow(() => hidden.Render(values));

        var invalidTransition = new LayoutRenderer("<stack id='hidden' visible='false' transitionMs='-1'><text id='child' color='invalid'>Hidden</text></stack>");
        var error = Assert.Throws<FormatException>(() => invalidTransition.Render(values))!;
        Assert.That(error.Message, Does.Contain("transitionMs").And.Contain("ID 'hidden'").And.Not.Contain("ID 'child'"));
    }

    [Test]
    public void ChildPaintErrorsKeepTheirLocationAheadOfParentPaintErrors()
    {
        const string xml = "<stack id='root' background='invalid'>\n  <layer id='middle'>\n    <text id='child' color='invalid'>Text</text>\n  </layer>\n</stack>";
        var error = Assert.Throws<FormatException>(() => new LayoutRenderer(xml).Render(DataHub.ParseValues("{}")))!;
        Assert.That(error.Message, Does.Contain("ID 'child'").And.Not.Contain("ID 'middle'").And.Not.Contain("ID 'root'"));
        Assert.That(error.Data["LayoutLine"], Is.EqualTo(3));
    }

    [Test]
    public void SegmentedChoicesKeepHiddenIndicesAndSuppressVisibleChildInputAndModalAnswers()
    {
        const string xml = "<segmented id='choices' key='choice' interactive='true'><layer id='hidden' visible='false'/><stack id='visible' interactive='true' closeModal='true' result='close'><text-field id='nested' key='text' interactive='true'/></stack></segmented>";
        var inputs = new List<ControlInput>();
        var answers = new List<string>();
        var renderer = new LayoutRenderer(xml);
        var view = new UiView(ButtonTests.Surface(), renderer.Render(DataHub.ParseValues("{\"choice\":0}"), input: inputs.Add,
            modal: true, modalAnswer: (id, answer) => { answers.Add(id); return answer; }));
        var root = view.Tree.Root;
        Assert.That(root.Children, Has.Count.EqualTo(2));
        Assert.That(root.Children[0].Id, Does.EndWith(".hidden"));
        Assert.That(root.Children[1].Id, Does.EndWith(".visible"));
        Assert.That(answers, Is.Empty);
        Assert.That(root.Children.SelectMany(ButtonTests.Nodes).Any(n => n.Properties.ContainsKey("events") || n.Properties.ContainsKey("answer")), Is.False);

        view.Dispatch(new UiEvent { NodeId = root.Id, Name = "change", Data = JsonSerializer.SerializeToElement(1) });
        Assert.That(inputs, Has.Count.EqualTo(1));
        Assert.That(inputs[0].ElementId, Is.EqualTo("choices"));
        Assert.That(inputs[0].Value, Is.EqualTo(1));
    }

    [Test]
    public void RepeatedTextHandlersRetainTheirOwnDataScopeAndCallbackAcrossRenders()
    {
        const string xml = "<repeat id='rows' items='rows' itemKey='id'><text-field id='entry' key='value_{{itemKey}}' interactive='true'/></repeat>";
        var renderer = new LayoutRenderer(xml);
        var firstInputs = new List<ControlInput>();
        var secondInputs = new List<ControlInput>();
        var first = new UiView(ButtonTests.Surface(), renderer.Render(DataHub.ParseValues("""{"rows":[{"id":"a"},{"id":"b"}],"value_a":"A","value_b":"B"}"""), input: firstInputs.Add));
        var second = new UiView(ButtonTests.Surface(), renderer.Render(DataHub.ParseValues("""{"rows":[{"id":"b"}],"value_b":"New B"}"""), input: secondInputs.Add));
        var firstFields = ButtonTests.Nodes(first.Tree.Root).Where(n => n.Type == "ui.text-field").ToArray();
        var secondField = ButtonTests.Nodes(second.Tree.Root).Single(n => n.Type == "ui.text-field");
        void Send(UiView view, string id, string text) => view.Dispatch(new UiEvent { NodeId = id, Name = "change", Data = JsonSerializer.SerializeToElement(text) });

        Send(first, firstFields[0].Id, "A1");
        Send(first, firstFields[1].Id, "B1");
        Send(first, firstFields[0].Id, "A2");
        Send(second, secondField.Id, "B2");

        Assert.That(firstInputs.Select(i => i.PreviousTextValue), Is.EqualTo(new[] { "A", "B", "A1" }));
        Assert.That(firstInputs.Select(i => i.DataKey), Is.EqualTo(new[] { "value_a", "value_b", "value_a" }));
        Assert.That(firstInputs.Select(i => i.RepeatedItem!.ItemKey), Is.EqualTo(new[] { "a", "b", "a" }));
        Assert.That(firstInputs.Select(i => i.RepeatedItem!.Index), Is.EqualTo(new[] { 0, 1, 0 }));
        Assert.That(secondInputs, Has.Count.EqualTo(1));
        Assert.That(secondInputs[0].PreviousTextValue, Is.EqualTo("New B"));
        Assert.That(secondInputs[0].RepeatedItem!.Index, Is.Zero);
        Assert.That(secondField.Id, Is.EqualTo(firstFields[1].Id));
    }

    [Test]
    public void FailedRenderReleasesUnvisitedAnimationTracksBeforeRecovery()
    {
        var clock = new Clock();
        var animation = new DisplayAnimation(clock);
        var renderer = new LayoutRenderer("<stack id='root'><text id='first' color='{{color}}'>Text</text><line id='line' length='{{length}}' transitionMs='1000' transitionProperties='length' easing='linear'/></stack>");
        double Render(string color, double length)
        {
            var view = new UiView(ButtonTests.Surface(), renderer.Render(DataHub.ParseValues(JsonSerializer.Serialize(new { color, length })), animation: animation));
            return ButtonTests.Nodes(view.Tree.Root).Single(n => n.Id.EndsWith(".line.row.body")).Properties["mainSize"].GetProperty("basis").GetDouble();
        }

        Render("#fff", .1);
        Render("#fff", .9);
        clock.Milliseconds = 500;
        Assert.That(Render("#fff", .9), Is.EqualTo(.5).Within(1e-10));
        Assert.Throws<FormatException>(() => Render("invalid", .9));
        Assert.That(Render("#fff", .9), Is.EqualTo(.9), "The failed frame must not leave the previous transition attached to an unvisited element.");
    }

    private sealed class Clock : TimeProvider
    {
        public long Milliseconds;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => Milliseconds;
    }
}
