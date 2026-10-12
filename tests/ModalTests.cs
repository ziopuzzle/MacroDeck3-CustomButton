using System.Text.Json;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Ui;
using MacroDeck.Ui.Model.Surfaces;
using MacroDeck.Plugin.Testing.Fakes;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class ModalTests
{
    [TestCase(null)]
    [TestCase("other-client")]
    public async Task SelectedDeviceWorksWithoutOriginAndOverridesOrigin(string? origin)
    {
        var integration = new CustomButtonIntegration();
        var host = new FakeIntegrationContext();
        await integration.InitializeAsync(host);
        var deviceId = Guid.NewGuid().ToString();
        var ui = new DialogUi();
        var executor = integration.Actions.Single(a => a.Id == "show-modal").CreateExecutor();
        var run = new ActionExecutionContext
        {
            Ui = ui, OriginClientId = origin, OwnerWidgetId = "widget",
            Parameters = new Dictionary<string, object>
            {
                ["deviceId"] = deviceId, ["channel"] = "details", ["title"] = "Details",
                ["layout"] = ShowModalAction.SampleLayout, ["initialValues"] = "{\"title\":\"Example\",\"value\":42}"
            }
        };
        foreach (var client in new[] { "selected-client", "reconnected-client" })
        {
            host.Deck.SeedClients(new MacroDeck.Sdk.Decks.DeckClient { ClientId = client, DeviceId = deviceId, ProfileId = "profile", FolderId = "folder" });
            Assert.That((await executor.ExecuteAsync(run)).Status, Is.EqualTo(ActionResultStatus.Succeeded));
            Assert.That(ui.Client, Is.EqualTo(client));
        }
        host.Deck.SeedClients();
        ui.Modal = null;
        Assert.That((await executor.ExecuteAsync(run)).ErrorCode, Is.EqualTo(ActionErrorCodes.Unavailable));
        Assert.That(ui.Modal, Is.Null, "An offline explicit target must not fall back to the origin.");
    }
    [Test]
    public async Task SampleRendersWithoutMissingIds()
    {
        await using var session = new ButtonSession(ButtonTests.Surface(), new("demo", ShowModalAction.SampleLayout, "{\"title\":\"Example\",\"value\":42}"), null);
        Assert.That(ButtonTests.Text(session.BuildTree(), ".valueLabel"), Is.EqualTo("42%"));
    }
    [Test]
    public async Task DialogPublishesPressAndSliderEventsUsingItsTarget()
    {
        var integration = new CustomButtonIntegration();
        var context = new FakeIntegrationContext();
        await integration.InitializeAsync(context);
        var surface = ButtonTests.Surface() with
        {
            Kind = UiSurfaceKinds.Dialog,
            Attributes = new Dictionary<string, JsonElement>
            {
                [UiDialogSurfaceAttributes.ViewId] = JsonSerializer.SerializeToElement(ShowModalAction.ViewId),
                [UiDialogSurfaceAttributes.Data] = JsonSerializer.SerializeToElement(new
                {
                    channel = "details", eventWidgetId = "target-widget", initialValues = "{\"value\":42}",
                    layout = "<stack id='details'><stack id='tap' interactive='true'><text id='label'>Tap</text></stack><slider id='slider' key='value' interactive='true'/></stack>"
                })
            }
        };
        await using var session = await integration.CreateSessionAsync(new() { Surface = surface, UiModelVersion = 1 }, default);
        var tree = session!.BuildTree();
        Assert.That(tree.Root.Type, Is.EqualTo("ui.stack"));
        var tap = ButtonTests.Nodes(tree.Root).Single(n => n.Id.EndsWith(".tap"));
        var slider = ButtonTests.Nodes(tree.Root).Single(n => n.Type == "ui.slider");
        session.Dispatch(new() { NodeId = tap.Id, Name = "press" });
        session.Dispatch(new() { NodeId = slider.Id, Name = "change", Data = JsonSerializer.SerializeToElement(.75) });
        Assert.That(context.Events.Published.Select(e => e.EventId), Is.EqualTo(new[] { "element-press", "element-change" }));
        foreach (var item in context.Events.Published)
            Assert.That(item.Parameters!.Value.GetProperty("widgetId").GetString(), Is.EqualTo("target-widget"));
        Assert.That(context.Events.Published.Last().Parameters!.Value.GetProperty("value").GetDouble(), Is.EqualTo(75));
    }
    private sealed class DialogUi : IUiInteractions
    {
        public ModalDefinition? Modal;
        public string? Client;
        public Task<bool> ShowModalAsync(string? originClientId, ModalDefinition modal, CancellationToken cancellationToken = default)
        { Client = originClientId; Modal = modal; return Task.FromResult(true); }
        public Task<ModalResult<T>> ShowModalAsync<T>(string? originClientId, ModalDefinition modal, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
    [Test]
    public async Task OpensLiteralXmlAndUpdatesDialogFromChannel()
    {
        var action = new ShowModalAction();
        var ui = new DialogUi();
        var result = await action.CreateExecutor().ExecuteAsync(new()
        {
            OriginClientId = "client", OwnerWidgetId = "source-widget", Ui = ui,
            Parameters = new Dictionary<string, object>
            {
                ["title"] = "Details", ["channel"] = "details",
                ["layout"] = "<text id=\"value\">{{value}}</text>", ["initialValues"] = "{\"value\":42}"
            }
        });
        Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Succeeded));
        Assert.That(ui.Client, Is.EqualTo("client"));
        Assert.That(ui.Modal!.Data!["eventWidgetId"].GetString(), Is.EqualTo("source-widget"));
        Assert.That(action.Parameters.Where(p => p.Name is "layout" or "initialValues").All(p => p.LiteralOnly), Is.True);
        var integration = new CustomButtonIntegration();
        var surface = ButtonTests.Surface() with
        {
            Kind = UiSurfaceKinds.Dialog,
            Attributes = new Dictionary<string, JsonElement>
            {
                [UiDialogSurfaceAttributes.ViewId] = JsonSerializer.SerializeToElement(ui.Modal!.ViewId),
                [UiDialogSurfaceAttributes.Data] = JsonSerializer.SerializeToElement(ui.Modal.Data)
            }
        };
        await using var session = await integration.CreateSessionAsync(new() { Surface = surface, UiModelVersion = 1 }, default);
        Assert.That(ButtonTests.Text(session!.BuildTree(), ".value"), Is.EqualTo("42"));
        integration.Hub.Update("details", DataHub.ParseValues("{\"value\":73}"));
        ((ButtonSession)session).Refresh();
        Assert.That(ButtonTests.Text(session.BuildTree(), ".value"), Is.EqualTo("73"));
    }
    [Test]
    public async Task BackendEventCannotOpenOnAnUnspecifiedClient()
    {
        var ui = new DialogUi();
        var result = await new ShowModalAction().CreateExecutor().ExecuteAsync(new() { Ui = ui, Parameters = new Dictionary<string, object>() });
        Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Failed));
        Assert.That(ui.Modal, Is.Null);
    }
}
