using System.Text.Json;
using MacroDeck.Sdk.Ui;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class PreviewRefreshTests
{
    [Test] public async Task EditorPreviewPublishesFreshValuesRepeatedlyWithoutLayoutChanges()
    {
        var integration = new CustomButtonIntegration();
        var attributes = new Dictionary<string, JsonElement>(ButtonTests.Surface("preview").Attributes);
        attributes.Remove("widgetId");
        attributes["variableScopeWidgetId"] = JsonSerializer.SerializeToElement("test-widget");
        attributes["data"] = JsonSerializer.SerializeToElement(new
        {
            channel = "preview-test", layout = new LayoutDocument(TestLayouts.SampleLayout).Serialize(),
            initialValues = "{\"value\":0}"
        });
        await using var preview = (await integration.CreateSessionAsync(new UiSessionRequest
        {
            Surface = ButtonTests.Surface("preview") with { Attributes = attributes }, UiModelVersion = 1
        }, default))!;
        var updates = System.Threading.Channels.Channel.CreateUnbounded<MacroDeck.Ui.Model.Patches.UiPatch>();
        preview.Changed += (_, _) => { foreach (var patch in preview.DrainPatches()) updates.Writer.TryWrite(patch); };
        var revision = preview.BuildTree().Revision;
        foreach (var value in new[] { 10, 20, 30 })
        {
            integration.Hub.Update("preview-test", DataHub.ParseValues($"{{\"value\":{value}}}"));
            var patch = await updates.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
            Assert.That(patch.FromRevision, Is.EqualTo(revision));
            Assert.That(patch.ToRevision, Is.GreaterThan(revision));
            Assert.That(patch.Operations.All(op => op.Op == "set-properties"), Is.True);
            Assert.That(patch.Operations.Single(op => op.NodeId.EndsWith(".value")).Properties!["text"].GetString(), Is.EqualTo(value + "%"));
            revision = patch.ToRevision;
        }
    }
}

