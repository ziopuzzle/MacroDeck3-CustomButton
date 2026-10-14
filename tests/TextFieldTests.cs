using System.Text.Json;
using MacroDeck.Plugin.Testing.Fakes;
using MacroDeck.Ui.Model.Surfaces;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class TextFieldTests
{
    private const string Layout = "<stack id='body'><text-field id='entry' key='message' placeholder='Type here' interactive='true'/><text id='echo'>{{message}}</text></stack>";

    [TestCase("widget")]
    [TestCase("dialog")]
    public async Task TextEventsUpdateDataAndReadPreviousValueWithoutWaitingForRedraw(string kind)
    {
        var integration = new CustomButtonIntegration();
        var host = new FakeIntegrationContext();
        await integration.InitializeAsync(host);
        var data = JsonSerializer.SerializeToElement(new { channel = "text", eventWidgetId = "owner", layout = Layout, initialValues = "{\"message\":\"Initial\"}" });
        var surface = ButtonTests.Surface(kind) with { Attributes = new Dictionary<string, JsonElement> {
            ["widgetId"] = JsonSerializer.SerializeToElement("owner"),
            ["widgetType"] = JsonSerializer.SerializeToElement(CustomButtonIntegration.TypeId),
            ["data"] = data,
            [UiDialogSurfaceAttributes.ViewId] = JsonSerializer.SerializeToElement(ShowModalAction.ViewId),
            [UiDialogSurfaceAttributes.Data] = data
        } };
        await using var session = (ButtonSession)(await integration.CreateSessionAsync(new() { Surface = surface, UiModelVersion = 1 }, default))!;
        var field = ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Type == "ui.text-field");
        Assert.That(field.Properties["text"].GetString(), Is.EqualTo("Initial"));
        void Send(string name, object text) => session.Dispatch(new() { NodeId = field.Id, Name = name, Data = JsonSerializer.SerializeToElement(text) });
        Send("adjust", " 日本語 😀 ");
        Send("adjust", "next");
        integration.Hub.Update("text", DataHub.ParseValues("{\"message\":\"external\"}"));
        Send("change", "");
        var events = host.Events.Published.Where(e => e.EventId.StartsWith("text-")).ToArray();
        Assert.That(events.Select(e => e.EventId), Is.EqualTo(new[] { "text-changing", "text-changing", "text-changed" }));
        Assert.That(events.Select(e => e.Parameters!.Value.GetProperty("previousValue").GetString()), Is.EqualTo(new[] { "Initial", " 日本語 😀 ", "external" }));
        Assert.That(events[0].Parameters!.Value.GetProperty("value").GetString(), Is.EqualTo(" 日本語 😀 "));
        Assert.That(events[0].Parameters!.Value.GetProperty("key").GetString(), Is.EqualTo("message"));
        Assert.That(events[0].Parameters!.Value.GetProperty("widgetId").GetString(), Is.EqualTo("owner"));
        Assert.That(integration.Hub.Snapshot("text").Values["message"].GetString(), Is.Empty);
        Send("change", 42);
        Send("adjust", new string('x', 16001));
        Assert.That(host.Events.Published.Count(e => e.EventId.StartsWith("text-")), Is.EqualTo(3));
        integration.Hub.Update("text", DataHub.ParseValues("{\"message\":\"After\"}"));
        session.Refresh();
        Assert.That(ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Type == "ui.text-field").Properties["text"].GetString(), Is.EqualTo("After"));
    }

    [Test]
    public async Task ReadOnlyLiteralPreservesWhitespace()
    {
        var inputs = new List<ControlInput>();
        await using var session = new ButtonSession(ButtonTests.Surface(), new("text", "<text-field id='entry' value='  hello  ' placeholder='  hint  '/>", "{}"), null, onControlInput: inputs.Add);
        var field = ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Type == "ui.text-field");
        Assert.That(field.Properties["text"].GetString(), Is.EqualTo("  hello  "));
        session.Dispatch(new() { NodeId = field.Id, Name = "change", Data = JsonSerializer.SerializeToElement("changed") });
        Assert.That(inputs, Is.Empty);
    }

    [TestCase("interactive='true'")]
    [TestCase("key='message' value='x'")]
    public void InvalidBindingsAreExplained(string attributes)
        => Assert.Throws<FormatException>(() => new LayoutRenderer($"<text-field id='entry' {attributes}/>").Render(DataHub.ParseValues("{}")));

    [Test]
    public void EditorAndEventsExposeTextField()
    {
        var document = new LayoutDocument("<stack id='root'/>");
        var id = document.Add("root", "text-field");
        Assert.That(document.Find(id).Attribute("key")!.Value, Is.EqualTo("text"));
        Assert.That(ButtonEvents.InputEvents(Layout, "entry").Select(e => e.Id), Is.EqualTo(new[] { "text-changing", "text-changed" }));
        foreach (var eventId in new[] { "text-changing", "text-changed" })
        {
            var flows = ButtonEvents.AddElementEvent(ButtonEvents.Empty, "owner", eventId, "entry", Layout);
            Assert.That(ButtonEvents.ElementTargetHelp(flows, Layout, "owner"), Does.Not.Contain("not a target"));
            var definition = ButtonEvents.Definitions.Single(e => e.Id == eventId);
            Assert.That(definition.PayloadParameters.Single(p => p.Name == "previousValue").Type, Is.EqualTo(MacroDeck.Sdk.Actions.ActionParameterType.String));
        }
    }
}
