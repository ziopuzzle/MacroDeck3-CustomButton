using System.Text.Json;
using MacroDeck.Plugin.Testing.Fakes;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Ui;
using MacroDeck.Ui.Model.Surfaces;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class ModalWaitTests
{
    [TestCase(false, true, false)]
    [TestCase(true, true, false)]
    [TestCase(true, false, false)]
    [TestCase(true, true, true)]
    public async Task WaitReportsOnlyOpenedSessions(bool cancelled, bool opened, bool abort)
    {
        var integration = new CustomButtonIntegration();
        var host = new FakeIntegrationContext();
        await integration.InitializeAsync(host);
        var ui = new WaitingUi(integration, opened);
        using var lifetime = new CancellationTokenSource();
        var run = integration.Actions.Single(a => a.Id == "show-modal").CreateExecutor().ExecuteAsync(new() {
            OriginClientId = "client", OwnerWidgetId = "owner", Ui = ui, CancellationToken = lifetime.Token,
            Parameters = new Dictionary<string, object> {
                ["channel"] = "modal-data", ["waitForClose"] = "true", ["eventWidgetId"] = "$self",
                ["layout"] = ShowModalAction.SampleLayout, ["initialValues"] = "{}"
            }
        });
        await ui.Ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(run.IsCompleted, Is.False);
        Assert.That(host.Events.Published.Any(e => e.EventId == ButtonEvents.ModalClosed), Is.False);
        if (abort) lifetime.Cancel();
        else ui.Result.SetResult(cancelled ? ModalResult.FromCancellation<string>() : ModalResult.FromValue("close"));
        if (abort) Assert.CatchAsync<OperationCanceledException>(async () => await run);
        else Assert.That((await run).Status, Is.EqualTo(opened ? ActionResultStatus.Succeeded : ActionResultStatus.Failed));
        var events = host.Events.Published.Where(e => e.EventId == ButtonEvents.ModalClosed).ToArray();
        Assert.That(events.Length, Is.EqualTo(opened ? 1 : 0));
        if (opened)
        {
            var payload = events[0].Parameters!.Value;
            Assert.That(payload.GetProperty("widgetId").GetString(), Is.EqualTo("owner"));
            Assert.That(payload.GetProperty("channel").GetString(), Is.EqualTo("modal-data"));
            Assert.That(payload.GetProperty("cancelled").GetBoolean(), Is.EqualTo(cancelled));
        }
        if (ui.Session != null) await ui.Session.DisposeAsync();
    }

    private sealed class WaitingUi(CustomButtonIntegration integration, bool opened) : IUiInteractions
    {
        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<ModalResult<string>> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IUiSession? Session;
        public Task<bool> ShowModalAsync(string? originClientId, ModalDefinition modal, CancellationToken cancellationToken = default)
            => throw new AssertionException("Expected the waiting API.");
        public async Task<ModalResult<T>> ShowModalAsync<T>(string? originClientId, ModalDefinition modal, CancellationToken cancellationToken = default)
        {
            if (opened)
                Session = await integration.CreateSessionAsync(new() { UiModelVersion = 1, Surface = ButtonTests.Surface("dialog") with {
                    Attributes = new Dictionary<string, JsonElement> {
                        [UiDialogSurfaceAttributes.ViewId] = JsonSerializer.SerializeToElement(modal.ViewId),
                        [UiDialogSurfaceAttributes.Data] = JsonSerializer.SerializeToElement(modal.Data)
                    }
                } }, cancellationToken);
            Ready.SetResult();
            ModalResult<string> result;
            try { result = await Result.Task.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) { result = ModalResult.FromCancellation<string>(); }
            return (ModalResult<T>)(object)result;
        }
    }
}
