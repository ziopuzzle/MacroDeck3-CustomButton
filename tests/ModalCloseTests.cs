using System.Text.Json;
using MacroDeck.Ui.Model.Surfaces;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class ModalCloseTests
{
    [Test]
    public void ResultSupportsConditionalBindingsAndLengthValidation()
    {
        var renderer = new LayoutRenderer("<layer id='finish' closeModal='true' result='default'><style when='ready == true' result='{{choice}}'/></layer>");
        var captured = new List<(string Id, string Value)>();
        string Register(string id, string value) { captured.Add((id, value)); return "token"; }
        renderer.Render(DataHub.ParseValues("{\"ready\":false}"), modal: true, modalAnswer: Register);
        renderer.Render(DataHub.ParseValues("{\"ready\":true,\"choice\":\" 日本語 \"}"), modal: true, modalAnswer: Register);
        Assert.That(captured, Is.EqualTo(new[] { ("finish", "default"), ("finish", " 日本語 ") }));
        var error = Assert.Throws<FormatException>(() => renderer.Render(
            DataHub.ParseValues(JsonSerializer.Serialize(new { ready = true, choice = new string('x', 4097) })), modal: true));
        Assert.That(error!.Message, Does.Contain("4096").And.Contain("finish"));
    }

    [Test]
    public async Task BoundCloseModeCanReturnToOrdinaryInteraction()
    {
        var hub = new DataHub();
        var inputs = new List<ControlInput>();
        await using var session = new ButtonSession(ButtonTests.Surface("dialog"), new("close-test",
            "<stack id='control' interactive='true'><style when='closing == true' closeModal='{{closing}}'/><text id='label'>Action</text></stack>",
            "{\"closing\":true}"), hub, onControlInput: inputs.Add);
        var close = ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Properties.ContainsKey("answer"));
        session.Dispatch(new() { NodeId = close.Id, Name = "press" });
        Assert.That(inputs, Is.Empty);
        hub.Update("close-test", DataHub.ParseValues("{\"closing\":false}"));
        session.Refresh();
        var nodes = ButtonTests.Nodes(session.BuildTree().Root).ToArray();
        Assert.That(nodes.Any(n => n.Properties.ContainsKey("answer")), Is.False);
        var control = nodes.Single(n => n.Properties.ContainsKey("events"));
        session.Dispatch(new() { NodeId = control.Id, Name = "press" });
        Assert.That(inputs.Count, Is.EqualTo(1));
    }

    [TestCase("stack", "")]
    [TestCase("stack", "background='#12345680' opacity='0.5' borderStyle='static'")]
    [TestCase("layer", "opacity='0.5'")]
    public async Task CloseClaimsOnlyPressOnTheFinalPaintNodeWithoutAnEventTarget(string type, string paint)
    {
        var integration = new CustomButtonIntegration();
        var data = JsonSerializer.SerializeToElement(new {
            channel = "modal", initialValues = "{}",
            layout = $"<{type} id='close' closeModal='true' interactive='true' {paint}><text id='label'>Close</text></{type}>"
        });
        var surface = ButtonTests.Surface("dialog") with { Attributes = new Dictionary<string, JsonElement> {
            [UiDialogSurfaceAttributes.ViewId] = JsonSerializer.SerializeToElement(ShowModalAction.ViewId),
            [UiDialogSurfaceAttributes.Data] = data
        } };
        await using var session = (ButtonSession)(await integration.CreateSessionAsync(new() { Surface = surface, UiModelVersion = 1 }, default))!;
        var nodes = ButtonTests.Nodes(session.BuildTree().Root).ToArray();
        var close = nodes.Single(n => n.Properties.ContainsKey("answer"));
        Assert.That(close.Properties["answer"].GetString(), Is.EqualTo("close"));
        Assert.That(close.Properties["events"].EnumerateArray().Select(e => e.GetString()), Is.EquivalentTo(new[] { "press" }));
    }

    [TestCase("widget")]
    [TestCase("preview")]
    public async Task CloseDoesNotClaimPressOutsideModal(string kind)
    {
        await using var session = new ButtonSession(ButtonTests.Surface(kind), new ButtonSettings(
            "modal", "<stack id='close' closeModal='true'><text id='label'>Close</text></stack>", "{}"), null);
        Assert.That(ButtonTests.Nodes(session.BuildTree().Root).Any(n => n.Properties.ContainsKey("answer")), Is.False);
    }
}
