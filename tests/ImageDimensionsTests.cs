using System.Buffers.Binary;
using MacroDeck.Ui.Runtime;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class ImageDimensionsTests
{
    [Test] public void ReadsPngGifAndJpegCanvasSizes()
    {
        var png = new byte[24]; "IHDR"u8.CopyTo(png.AsSpan(12));
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(16), 600);
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(20), 300);
        Assert.That(ImageDimensions.AspectRatio(png, "image/png"), Is.EqualTo(2));
        byte[] gif = [71, 73, 70, 56, 57, 97, 100, 0, 200, 0];
        Assert.That(ImageDimensions.AspectRatio(gif, "image/gif"), Is.EqualTo(.5));
        // APP segment before a progressive SOF, including padding FF bytes.
        byte[] jpeg = [0xff, 0xd8, 0xff, 0xe1, 0, 4, 0, 0, 0xff, 0xff, 0xc2, 0, 8, 8, 0, 100, 1, 44, 1];
        Assert.That(ImageDimensions.AspectRatio(jpeg, "image/jpeg"), Is.EqualTo(3));
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(20), 0);
        Assert.That(ImageDimensions.AspectRatio(png, "image/png"), Is.EqualTo(1));
    }
    private static byte[] Webp(string kind, byte[] payload)
    {
        var result = new byte[20 + payload.Length];
        "RIFF"u8.CopyTo(result); "WEBP"u8.CopyTo(result.AsSpan(8));
        System.Text.Encoding.ASCII.GetBytes(kind).CopyTo(result, 12);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(16), (uint)payload.Length);
        payload.CopyTo(result, 20); return result;
    }
    [Test] public void ReadsAllThreeWebpHeaders()
    {
        Assert.That(ImageDimensions.AspectRatio(Webp("VP8X", [0, 0, 0, 0, 199, 0, 0, 99, 0, 0]), "image/webp"), Is.EqualTo(2));
        var lossless = new byte[5]; lossless[0] = 0x2f;
        BinaryPrimitives.WriteUInt32LittleEndian(lossless.AsSpan(1), 199 | (99u << 14));
        Assert.That(ImageDimensions.AspectRatio(Webp("VP8L", lossless), "image/webp"), Is.EqualTo(2));
        Assert.That(ImageDimensions.AspectRatio(Webp("VP8 ", [0, 0, 0, 0x9d, 1, 0x2a, 200, 0, 100, 0]), "image/webp"), Is.EqualTo(2));
    }
    [Test] public void TruncatedOrUnknownHeadersNeverReadBeyondTheInput()
    {
        foreach (var type in new[] { "image/png", "image/jpeg", "image/gif", "image/webp", "unknown" })
            for (var n = 0; n < 40; n++) Assert.That(ImageDimensions.AspectRatio(new byte[n], type), Is.EqualTo(1));
        var truncated = Webp("VP8X", [0, 0]);
        BinaryPrimitives.WriteUInt32LittleEndian(truncated.AsSpan(16), uint.MaxValue);
        Assert.That(ImageDimensions.AspectRatio(truncated, "image/webp"), Is.EqualTo(1));
    }
    [TestCase(2, 2)][TestCase(.5, 2)][TestCase(1, 1)][TestCase(0, 1)]
    public void CoverClipsAScaledImageWithoutAddingABackgroundOrPressEvents(double aspect, double zoom)
    {
        var element = new LayoutRenderer("<image id='cover' size='60%' fit='cover' opacity='0.4'/>").Render(DataHub.ParseValues("{}"), imageAspectRatio: _ => aspect);
        var root = new UiView(ButtonTests.Surface(), element).Tree.Root;
        var nodes = ButtonTests.Nodes(root).ToArray();
        Assert.That(root.Properties["clip"].GetString(), Is.EqualTo("bounds"));
        Assert.That(nodes.Single(n => n.Type == "ui.transform").Properties["zoom"].GetDouble(), Is.EqualTo(zoom));
        Assert.That(nodes.Count(n => n.Type == "ui.image"), Is.EqualTo(1));
        Assert.That(nodes.Single(n => n.Type == "ui.image").Properties["opacity"].GetDouble(), Is.EqualTo(.4));
        Assert.That(nodes.Any(n => n.Type == "ui.button" || n.Properties.ContainsKey("background")), Is.False);
    }
}

