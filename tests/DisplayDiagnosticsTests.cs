using System.Text.Json;
using MacroDeck.Ui.Config;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class DisplayDiagnosticsTests
{
    [Test] public async Task SettingsExposeCopyableDraftErrorsAndRefreshAfterCorrection()
    {
        await using var session = new ConfigurationSession(ButtonTests.Surface("config"), TestLayouts.Sample with { Layout = "<text id='bad' size='invalid'>Hello</text>" });
        string Report() => ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id.EndsWith(".errorReport")).Properties["value"].GetString()!;
        Assert.That(Report(), Does.Contain("bad").And.Contain("line 1"));
        string[] Errors() => ButtonTests.Nodes(session.BuildTree().Root)
            .Where(n => n.Type == "prose" && n.Id.Contains(".error", StringComparison.Ordinal))
            .Select(n => LocalizationTests.Resolve(n.Properties["text"])).ToArray();
        Assert.That(Errors(), Has.Length.EqualTo(1));
        Assert.That(Errors()[0], Does.Contain("bad").And.Contain("line 1").And.Not.Contain("Checked:"));
        session.Dispatch(new() { NodeId = "layout", Name = "change", Data = JsonSerializer.SerializeToElement("<text id='good'>Hello</text>") });
        var refresh = ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id.EndsWith(".refreshDiagnostics"));
        session.Dispatch(new() { NodeId = refresh.Id, Name = UiConfigEvents.Activate });
        Assert.That(Report(), Does.Contain("check passed").And.Not.Contain("Current draft error"));
        Assert.That(Errors(), Is.Empty);
        var nodes = ButtonTests.Nodes(session.BuildTree().Root).ToArray();
        Assert.That(nodes.Single(n => n.Id.EndsWith(".diagnosticsStatus")).Properties["severity"].GetString(), Is.EqualTo("success"));
        var tab = nodes.Single(n => n.Id.EndsWith(".diagnosticsTab"));
        Assert.That(tab.Children.Any(n => n.Id.EndsWith(".errorReport")), Is.True, "Copy report is always visible, without a switch.");
        Assert.That(tab.Children.Any(n => n.Type == "advanced-section"), Is.False);
    }

    [Test] public void DraftPreviewAndWidgetFailuresShareOneMessageWithSeparateSourceDetails()
    {
        var reports = new DisplayDiagnostics();
        for (var i = 0; i < 6; i++) reports.Report("session" + i, "widget", "music", "preview", "Repeated error");
        reports.Report("different", "widget", "music", "preview", "Different error");
        reports.Report("channel", "widget", "other", "preview", "Repeated error");
        reports.Report("live", "widget", "music", "widget", "Repeated error");
        var report = reports.Read("widget", "Repeated error");
        Assert.That(reports.ReadSnapshot("widget", "Repeated error").Errors, Is.EqualTo(new[] { "Repeated error", "Different error" }));
        Assert.That(report.Split("Repeated error").Length - 1, Is.EqualTo(1));
        Assert.That(report, Does.Contain("6 session(s)").And.Contain("Different error").And.Contain("Channel: other")
            .And.Contain("Detected in current draft.").And.Contain("Last observed widget:").And.Contain("Last observed preview:"));
        reports.Report("session0", "widget", "music", "preview", null);
        Assert.That(reports.Read("widget"), Does.Not.Contain("Last observed preview:").And.Contain("Last observed widget:"));
    }

    [Test] public void ReplacementPreviewRecoveryClearsTypingErrorsButPreservesOtherWidgetsAndLiveErrors()
    {
        var reports = new DisplayDiagnostics();
        reports.Report("old1", "edited", "old-channel", "preview", "Incomplete expression");
        reports.Report("old2", "edited", "new-channel", "preview", "Missing parenthesis");
        reports.Report("live", "edited", "new-channel", "widget", "Live error");
        reports.Report("other", "another", "new-channel", "preview", "Other error");
        reports.Report("replacement", "edited", "new-channel", "preview", null);
        Assert.That(reports.ReadSnapshot("edited").Errors, Is.EqualTo(new[] { "Live error" }));
        Assert.That(reports.Read("another"), Does.Contain("Other error"));
        reports.Report("new-failure", "edited", "new-channel", "preview", "New error");
        Assert.That(reports.Read("edited"), Does.Contain("New error"));
    }

    [Test] public async Task RuntimeErrorsAreIsolatedAndClearedOnRecovery()
    {
        var reports = new DisplayDiagnostics(); var hub = new DataHub();
        var settings = TestLayouts.Sample with { Layout = "<text id='label' size='{{size}}'>Hello</text>", InitialValues = "{\"size\":\"invalid\"}" };
        await using var session = new ButtonSession(ButtonTests.Surface(), settings, hub,
            reportError: error => reports.Report("session", "widget", settings.Channel, "widget", error));
        Assert.That(reports.Read("widget"), Does.Contain("label").And.Contain("line 1"));
        Assert.That(reports.Read("another-widget"), Is.Empty);
        reports.Report("preview", "widget", settings.Channel, "preview", null);
        Assert.That(reports.Read("widget"), Is.Not.Empty);
        hub.Update(settings.Channel, DataHub.ParseValues("{\"size\":0.2}")); session.Refresh();
        Assert.That(reports.Read("widget"), Is.Empty);
    }
}
