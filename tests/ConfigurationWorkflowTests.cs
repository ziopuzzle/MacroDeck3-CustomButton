using System.Text.Json;
using MacroDeck.Ui.Model.Nodes;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class ConfigurationWorkflowTests
{
    private static UiNode Node(ConfigurationSession session, string id) => ButtonTests.Nodes(session.BuildTree().Root)
        .Single(n => n.Id == id || n.Id.EndsWith("." + id, StringComparison.Ordinal));
    private static string Text(ConfigurationSession session, string id) => LocalizationTests.Resolve(Node(session, id).Properties["text"]);
    private static void Change(ConfigurationSession session, string id, string value)
        => session.Dispatch(new() { NodeId = Node(session, id).Id, Name = "change", Data = JsonSerializer.SerializeToElement(value) });
    private static void Activate(ConfigurationSession session, string id)
        => session.Dispatch(new() { NodeId = Node(session, id).Id, Name = "activate" });
    private static bool HasRestore(ConfigurationSession session) => ButtonTests.Nodes(session.BuildTree().Root).Any(n => n.Id.EndsWith(".restoreTemplate"));
    private static ButtonSettings Settings => TestLayouts.Sample with { ConfigurationWidgetId = "test-widget" };

    [TestCase("{}")] [TestCase("[{\"triggerType\":\"unsupported\"}]")]
    public async Task RejectedActionEditsEchoLastAcceptedActionsToTheOptimisticEditor(string invalid)
    {
        await using var session = new ConfigurationSession(ButtonTests.Surface("config"), Settings with {
            Flows = ButtonEvents.AddDisplayTick(ButtonEvents.Empty, "test-widget") });
        var before = Node(session, "flows").Properties["value"].GetRawText();
        session.DrainPatches();
        session.Dispatch(new() { NodeId = "flows", Name = "change", Data = JsonSerializer.Deserialize<JsonElement>(invalid) });

        Assert.That(Node(session, "flows").Properties["value"].GetRawText(), Is.EqualTo(before));
        Assert.That(Text(session, "flowMessage"), Does.Contain("Action changes rejected").And.Contain("last valid actions"));
        var corrected = session.DrainPatches().SelectMany(p => p.Operations).Last(p => p.NodeId == "flows" && p.Properties?.ContainsKey("value") == true);
        Assert.That(corrected.Properties!["value"].GetRawText(), Is.EqualTo(before));
    }

    [Test]
    public async Task TemplateRecoveryRestoresBothDrawingFieldsAndKeepsCurrentChannelAndActions()
    {
        var settings = Settings with { InitialValues = " { \"title\": \"Original\", \"value\": 7 } " };
        await using var session = new ConfigurationSession(ButtonTests.Surface("config"), settings);
        Assert.That(Node(session, "templatePicker").Properties["description"].GetString(), Does.Contain("Replaces"));
        Change(session, "templatePicker", "basic_bar");
        Assert.That(HasRestore(session), Is.True);
        Assert.That(Node(session, "layout").Properties["value"].GetString(), Is.EqualTo(LayoutTemplates.Get("basic_bar").Xml));
        Change(session, "channel", "changed-channel");
        var actions = ButtonEvents.AddDisplayTick(ButtonEvents.Empty, "test-widget");
        session.Dispatch(new() { NodeId = "flows", Name = "change", Data = actions });
        var currentActions = Node(session, "flows").Properties["value"].GetRawText();

        Activate(session, "restoreTemplate");
        Assert.That(Node(session, "layout").Properties["value"].GetString(), Is.EqualTo(settings.Layout));
        Assert.That(Node(session, "initialValues").Properties["value"].GetString(), Is.EqualTo(settings.InitialValues));
        Assert.That(Node(session, "channel").Properties["value"].GetString(), Is.EqualTo("changed-channel"));
        Assert.That(Node(session, "flows").Properties["value"].GetRawText(), Is.EqualTo(currentActions));
        Assert.That(HasRestore(session), Is.False);
    }

    [Test]
    public async Task TemplateRecoveryReturnsToTheImmediatelyPreviousSelection()
    {
        await using var session = new ConfigurationSession(ButtonTests.Surface("config"), Settings);
        Change(session, "templatePicker", "basic_bar");
        Change(session, "templatePicker", "basic_text");
        Activate(session, "restoreTemplate");
        var previous = LayoutTemplates.Get("basic_bar");
        Assert.That(Node(session, "layout").Properties["value"].GetString(), Is.EqualTo(previous.Xml));
        Assert.That(Node(session, "initialValues").Properties["value"].GetString(), Is.EqualTo(previous.InitialValues));
        Assert.That(HasRestore(session), Is.False);
    }

    [TestCase("layout", "<text id='manual'>Keep this edit</text>")]
    [TestCase("initialValues", "{\"manual\":true}")]
    public async Task EditingEitherDrawingFieldInvalidatesTemplateRecovery(string field, string manual)
    {
        await using var session = new ConfigurationSession(ButtonTests.Surface("config"), Settings);
        Change(session, "templatePicker", "basic_bar");
        var staleRestoreId = Node(session, "restoreTemplate").Id;
        Change(session, field, manual);
        Assert.That(HasRestore(session), Is.False);
        session.Dispatch(new() { NodeId = staleRestoreId, Name = "activate" });
        Assert.That(Node(session, field).Properties["value"].GetString(), Is.EqualTo(manual));
    }

    [TestCase("layout", "<text id='bad' size='invalid'>Text</text>")]
    [TestCase("initialValues", "not json")]
    [TestCase("channel", "invalid channel")]
    public async Task DiagnosticsClearlyStayStaleUntilExplicitlyRefreshed(string field, string invalid)
    {
        await using var session = new ConfigurationSession(ButtonTests.Surface("config"), Settings);
        Assert.That(Node(session, "diagnosticsStatus").Properties["severity"].GetString(), Is.EqualTo("success"));
        var previousReport = Node(session, "errorReport").Properties["value"].GetString();
        Change(session, field, invalid);
        Assert.That(Node(session, "diagnosticsStatus").Properties["severity"].GetString(), Is.EqualTo("warning"));
        Assert.That(Text(session, "diagnosticsStatus"), Does.Contain("Draft changed").And.Contain("Refresh diagnostics"));
        Assert.That(Node(session, "errorReport").Properties["value"].GetString(), Is.EqualTo(previousReport));

        Activate(session, "refreshDiagnostics");
        Assert.That(Node(session, "diagnosticsStatus").Properties["severity"].GetString(), Is.EqualTo("error"));
        Assert.That(Text(session, "diagnosticsStatus"), Does.Contain("rendering check failed"));
    }
}
