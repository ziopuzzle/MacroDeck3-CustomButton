using System.Text.Json;
using MacroDeck.Sdk.Actions;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class TextDataTests
{
    [Test] public async Task PastedNamesAreTrimmedWithoutChangingSongText()
    {
        var hub = new DataHub();
        var title = "  A song\n";
        await using var session = new ButtonSession(ButtonTests.Surface(),
            new ButtonSettings("music_player", "<text id='title'>{{title}}</text>", "{\"title\":\"\"}"), hub);
        var result = await new UpdateValueAction(hub).CreateExecutor().ExecuteAsync(new ActionExecutionContext
        {
            Parameters = new Dictionary<string, object>
            { ["channel"] = " music_player\r\n", ["key"] = "title\n", ["value"] = title }
        });
        Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Succeeded));
        session.Refresh();
        Assert.That(ButtonTests.Text(session.BuildTree(), ".title"), Is.EqualTo(title));
        Assert.That(hub.Read("music_player").Keys, Is.EquivalentTo(new[] { "title" }));
        var jsonResult = await new UpdateValuesAction(hub).CreateExecutor().ExecuteAsync(new ActionExecutionContext
        {
            Parameters = new Dictionary<string, object>
            { ["channel"] = "music_player\n", ["json"] = "{\"title\":\"Next song\"}" }
        });
        Assert.That(jsonResult.Status, Is.EqualTo(ActionResultStatus.Succeeded));
        session.Refresh();
        Assert.That(ButtonTests.Text(session.BuildTree(), ".title"), Is.EqualTo("Next song"));
    }

    [TestCase("title\n")][TestCase("title\r\n")][TestCase("ti\ntle")]
    public void RawDataNamesMustMatchTheWholeString(string name)
    {
        Assert.Throws<FormatException>(() => DataHub.ValidateName(name));
        Assert.Throws<FormatException>(() => DataHub.ParseValues(JsonSerializer.Serialize(new Dictionary<string, string> { [name] = "Song" })));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task TrackNamesReachLiveTextPatchesAsStrings(bool jsonParameter)
    {
        var hub = new DataHub();
        var settings = new ButtonSettings("music", "<text id='track'>{{track}}</text>", "{\"track\":\"Initial\"}");
        await using var session = new ButtonSession(ButtonTests.Surface(), settings, hub);
        var patches = System.Threading.Channels.Channel.CreateUnbounded<bool>();
        session.Changed += (_, _) => patches.Writer.TryWrite(true);
        foreach (var title in new[] { "A new song", "日本語の曲名 & <Live> \"Mix\"", "Another song", "" })
        {
            var result = await new UpdateValueAction(hub).CreateExecutor().ExecuteAsync(new ActionExecutionContext
            {
                Parameters = new Dictionary<string, object>
                {
                    ["channel"] = "music", ["key"] = "track",
                    ["value"] = jsonParameter ? JsonSerializer.SerializeToElement(title) : title
                }
            });
            Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Succeeded));
            await patches.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
            Assert.That(hub.Read("music")["track"].GetString(), Is.EqualTo(title));
            var patch = session.DrainPatches().Single();
            Assert.That(ButtonTests.Text(session.BuildTree(), ".track"), Is.EqualTo(title));
            Assert.That(patch.Operations.Single().Properties!["text"].GetString(), Is.EqualTo(title));
        }
    }
}

