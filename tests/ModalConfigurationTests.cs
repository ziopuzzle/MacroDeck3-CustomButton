using System.Text.Json;
using MacroDeck.Sdk.Actions;
using MacroDeck.Ui.Model.Nodes;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class ModalConfigurationTests
{
    private static ActionConfigurationRequest Request() => new()
    {
        // Action config is not supplied with a Custom Button widget's surface attributes.
        Session = new() { UiModelVersion = 1, Surface = ButtonTests.Surface("config") with { Attributes = new Dictionary<string, JsonElement>() } },
        Parameters = new Dictionary<string, JsonElement>
        {
            ["title"] = JsonSerializer.SerializeToElement("{{vars.title}}"),
            ["channel"] = JsonSerializer.SerializeToElement("music"),
            ["deviceId"] = JsonSerializer.SerializeToElement("device"),
            ["eventWidgetId"] = JsonSerializer.SerializeToElement("$self"),
            ["layout"] = JsonSerializer.SerializeToElement(ShowModalAction.SampleLayout),
            ["initialValues"] = JsonSerializer.SerializeToElement("{}")
        }
    };
    private static UiNode Node(ModalConfigurationSession session, string id) => ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id == id);

    [Test]
    public async Task ActionProvidesConfigWithoutCustomButtonOwnerAndKeepsParameterIds()
    {
        var action = new ShowModalAction();
        await using var session = (ModalConfigurationSession)(await action.CreateConfigurationSessionAsync(Request(), default))!;
        foreach (var p in Request().Parameters)
            Assert.That(Node(session, p.Key).Properties["value"].GetString(), Is.EqualTo(p.Value.GetString()));
        Assert.That(Node(session, "deviceId").Type, Is.EqualTo("device-picker"));
        Assert.That(Node(session, "waitForClose").Properties["value"].GetString(), Is.EqualTo("false"));
        session.Dispatch(new() { NodeId = "waitForClose", Name = "change", Data = JsonSerializer.SerializeToElement("true") });
        Assert.That(Node(session, "waitForClose").Properties["value"].GetString(), Is.EqualTo("true"));
        Assert.That(Node(session, "eventWidgetId").Properties["options"][0].GetProperty("value").GetString(), Is.EqualTo("$self"));
        Assert.That(Node(session, "layout").Properties["literalOnly"].GetBoolean(), Is.True);
        Assert.That(Node(session, "initialValues").Properties["literalOnly"].GetBoolean(), Is.True);
        Assert.That(ButtonTests.Nodes(session.BuildTree().Root).Any(n => n.Id.EndsWith(".saveReminder") || n.Id.EndsWith(".editorStatus")), Is.False);
    }

    [Test]
    public async Task ExternalEditsRequireAClientChangeCarryingXmlAndRetainOtherParameters()
    {
        var editor = new Editor();
        await using var session = new ModalConfigurationSession(Request(), editor);
        session.BuildTree();
        session.Dispatch(new() { NodeId = ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id.EndsWith(".openModalEditor")).Id, Name = "activate" });
        Assert.That(editor.Layout, Is.EqualTo(ShowModalAction.SampleLayout));
        const string xml = "<scroll id='page'><text id='title'>Edited</text></scroll>";
        editor.Apply!(xml);
        Assert.That(editor.Current!(), Is.EqualTo(xml));
        Assert.That(Node(session, "layout").Type, Is.EqualTo("choice"));
        Assert.That(Node(session, "layout").Properties["segmented"].GetBoolean(), Is.True);
        Assert.That(Node(session, "layout").Properties["options"][0].GetProperty("value").GetString(), Is.EqualTo(xml));
        Assert.That(Node(session, "layout").Properties["options"][1].GetProperty("value").GetString(), Is.EqualTo(ShowModalAction.SampleLayout));
        Assert.That(ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id.EndsWith(".pendingWarning")).Properties["severity"].GetString(), Is.EqualTo("warning"));
        Assert.That(session.DrainPatches(), Is.Not.Empty);
        // This is the user-originated event that the host action card stores in layout.
        session.Dispatch(new() { NodeId = "layout", Name = "change", Data = JsonSerializer.SerializeToElement(xml) });
        Assert.That(Node(session, "layout").Type, Is.EqualTo("code"));
        Assert.That(ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id.EndsWith(".saveReminder")).Properties["severity"].GetString(), Is.EqualTo("warning"));
        Assert.That(Node(session, "layout").Properties["value"].GetString(), Is.EqualTo(xml));
        Assert.That(Node(session, "channel").Properties["value"].GetString(), Is.EqualTo("music"));
        session.Dispatch(new() { NodeId = "layout", Name = "change", Data = JsonSerializer.SerializeToElement("<stack id='manual'/>") });
        Assert.That(editor.Current!(), Is.EqualTo("<stack id='manual'/>"), "Native editor detects concurrent XML changes using this basis.");
    }

    [Test]
    public async Task CancelAndDisposeDoNotApplyPendingEdits()
    {
        var editor = new Editor();
        var session = new ModalConfigurationSession(Request(), editor);
        session.Dispatch(new() { NodeId = ButtonTests.Nodes(session.BuildTree().Root).Single(n => n.Id.EndsWith(".openModalEditor")).Id, Name = "activate" });
        editor.Apply!("<stack id='changed'/>");
        session.Dispatch(new() { NodeId = "layout", Name = "change", Data = JsonSerializer.SerializeToElement(ShowModalAction.SampleLayout) });
        Assert.That(Node(session, "layout").Properties["value"].GetString(), Is.EqualTo(ShowModalAction.SampleLayout));
        await session.DisposeAsync();
        Assert.That(editor.Disposed, Is.True);
        editor.Apply!("<stack id='late'/>");
        Assert.That(Node(session, "layout").Properties["value"].GetString(), Is.EqualTo(ShowModalAction.SampleLayout));
    }

    private sealed class Editor : ILayoutEditorSession
    {
        public string? Layout;
        public Func<string>? Current;
        public Action<string>? Apply;
        public bool Disposed;
        public void Open(string layout, string values, Func<string> currentLayout, Action<string> apply, Action<string> status)
        { Layout = layout; Current = currentLayout; Apply = apply; }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
}
