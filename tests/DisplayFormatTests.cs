using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class DisplayFormatTests
{
    [TestCase("0", "duration", "0:00")]
    [TestCase("0.9999", "duration", "0:00")]
    [TestCase("185.999", "duration", "3:05")]
    [TestCase("3599.999", "duration", "59:59")]
    [TestCase("3600", "duration", "1:00:00")]
    [TestCase("3723", "duration:mm:ss", "62:03")]
    [TestCase("185", "duration:hh:mm:ss", "0:03:05")]
    [TestCase("90061", "duration:hh:mm:ss", "25:01:01")]
    [TestCase("-185.999", "duration", "-3:05")]
    [TestCase("-0.999", "duration", "0:00")]
    [TestCase("5", "00", "05")]
    [TestCase("5.1", "000.00", "005.10")]
    public void FormatsBoundaries(string value, string format, string expected)
        => Assert.That(DisplayFormat.Apply(value, format), Is.EqualTo(expected));

    [TestCase("NaN", "duration")][TestCase("Infinity", "duration")]
    [TestCase("Song", "duration")][TestCase("1e20", "duration")]
    [TestCase("1", "duration:invalid")][TestCase("1", "F9")]
    public void RejectsInvalidFormatsAndDurations(string value, string format)
        => Assert.Throws<FormatException>(() => DisplayFormat.Apply(value, format));

    [TestCase("{{= floor(position / 1000):duration}} / {{= floor(duration / 1000):duration}}", "1:02 / 3:05")]
    [TestCase("{{= floor(max(duration - position, 0) / 1000):duration}}", "2:03")]
    [TestCase("{{= position / duration * 100:F1}}%", "33.5%")]
    [TestCase("{{= floor(position / 1000) % 60:00}}", "02")]
    [TestCase("{{= missing + 1:duration}}", "—")]
    [TestCase("{{missing:duration}}", "—")]
    public void FormatsBindingsAndExpressions(string input, string expected)
        => Assert.That(LayoutRenderer.Expand(input, DataHub.ParseValues("{\"position\":\"62000\",\"duration\":185000}")), Is.EqualTo(expected));

    [Test] public async Task PlaybackTextUpdatesWithLiveMilliseconds()
    {
        var hub = new DataHub();
        var settings = new ButtonSettings("music", "<text id='time'>{{= floor(position / 1000):duration}} / {{= floor(duration / 1000):duration}}</text>", "{\"position\":0,\"duration\":185000}");
        await using var session = new ButtonSession(ButtonTests.Surface(), settings, hub);
        Assert.That(ButtonTests.Text(session.BuildTree(), ".time"), Is.EqualTo("0:00 / 3:05"));
        hub.Update("music", DataHub.ParseValues("{\"position\":62000}")); session.Refresh();
        var patch = session.DrainPatches().Single();
        Assert.That(ButtonTests.Text(session.BuildTree(), ".time"), Is.EqualTo("1:02 / 3:05"));
        Assert.That(patch.Operations.Single().Properties!["text"].GetString(), Is.EqualTo("1:02 / 3:05"));
    }
}

