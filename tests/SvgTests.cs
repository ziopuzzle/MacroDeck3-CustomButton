using System.Xml.Linq;
using MacroDeck.Plugin.Testing.Fakes;
using NUnit.Framework;
using SkiaSharp;

namespace Ziopuzzle.CustomButton.Tests;

public class SvgTests
{
    private const string Template = "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='50'><rect width='{{width}}' height='50' fill='{{color}}'/><text x='10' y='30'>{{title}}</text></svg>";
    private static string Source(string json) => SvgTemplate.Source(Template, s => LayoutRenderer.Expand(s, DataHub.ParseValues(json)), 128);

    [Test] public void VariablesAreXmlEscapedAndExpressionsWork()
    {
        var source = Source("""{"width":50,"color":"#ff0000","title":"A & <B> \"C\""}""");
        var xml = XDocument.Parse(source[(source.IndexOf('\n') + 1)..]);
        Assert.That(xml.Descendants().Single(n => n.Name.LocalName == "text").Value, Is.EqualTo("A & <B> \"C\""));
        Assert.That(xml.Descendants().Count(), Is.EqualTo(3));
        var expression = SvgTemplate.Source("<svg width='{{= value * 2}}' height='50'/>", s => LayoutRenderer.Expand(s, DataHub.ParseValues("{\"value\":25}")), 128);
        Assert.That(expression, Does.Contain("width=\"50\""));
    }

    [TestCase("<svg><script/></svg>")]
    [TestCase("<svg><image href='https://example.invalid/a'/></svg>")]
    [TestCase("<svg><rect fill='url(https://example.invalid/a)'/></svg>")]
    [TestCase("<svg><style>@import 'https://example.invalid/a';</style></svg>")]
    [TestCase("<!DOCTYPE svg [<!ENTITY x SYSTEM 'file:///secret'>]><svg>&x;</svg>")]
    public void ExternalAndExecutableContentIsRejected(string svg) => Assert.Throws<FormatException>(() => SvgTemplate.Parse(svg));

    [Test] public async Task RasterizationPreservesTransparencyAspectAndUpdatesPixels()
    {
        using var http = new HttpClient();
        foreach (var color in new[] { "#ff0000", "#00ff00" })
        {
            var image = await ImageSourceLoader.LoadAsync(Source($$"""{"width":50,"color":"{{color}}","title":""}"""), http, 2 * 1024 * 1024, default);
            using var bitmap = SKBitmap.Decode(image.Bytes);
            Assert.That((bitmap.Width, bitmap.Height), Is.EqualTo((128, 64)));
            Assert.That(bitmap.GetPixel(10, 10), Is.EqualTo(SKColor.Parse(color)));
            Assert.That(bitmap.GetPixel(100, 10).Alpha, Is.Zero);
        }
    }

    [Test] public async Task SvgResourcesAreReplacedOnlyWhenValuesChange()
    {
        var context = new FakeIntegrationContext(); using var http = new HttpClient();
        await using var images = new SessionImages(context.UiResources, http);
        var red = Source("""{"width":50,"color":"#ff0000","title":"Test"}""");
        var green = Source("""{"width":50,"color":"#00ff00","title":"Test"}""");
        async Task WaitFor(Func<bool> ready)
        {
            for (var i = 0; i < 200; i++) { if (ready()) return; await Task.Delay(25); }
            Assert.Fail("SVG resource did not finish rendering.");
        }
        await WaitFor(() => images.Resolve("svg", red) != null);
        var first = images.Resolve("svg", red)!;
        var revision = images.Revision;
        Assert.That(images.NeedsRefresh, Is.False);
        images.Resolve("svg", red);
        Assert.That(images.Revision, Is.EqualTo(revision));
        images.Resolve("svg", green);
        await WaitFor(() => { images.Resolve("svg", green); return images.Revision > revision; });
        Assert.That(images.Resolve("svg", green)!.ContentHash, Is.Not.EqualTo(first.ContentHash));
    }

    [Test] public void EditorRoundTripPreservesSvgAndRendersTemplate()
    {
        var document = new LayoutDocument("<stack id='root'/>");
        var id = document.Add("root", "svg");
        Assert.That(new LayoutDocument(document.Serialize()).Find(id).Value, Does.Contain("<svg"));
        var template = LayoutTemplates.Get("basic_svg");
        string? source = null;
        new LayoutRenderer(template.Xml).Render(DataHub.ParseValues(template.InitialValues), images: (_, s) => { source = s; return null; });
        Assert.That(source, Does.StartWith(SvgTemplate.Prefix));
        Assert.That(source, Does.Contain("width=\"130\""));
    }

    [Test] public async Task ExampleRendersTextAndGradients()
    {
        var template = LayoutTemplates.Get("basic_svg");
        string? source = null;
        new LayoutRenderer(template.Xml).Render(DataHub.ParseValues(template.InitialValues), images: (_, s) => { source = s; return null; });
        var image = await SvgRasterizer.RenderAsync(source!, 2 * 1024 * 1024, default);
        using var bitmap = SKBitmap.Decode(image.Bytes);
        Assert.That(bitmap.Width, Is.EqualTo(512));
        Assert.That(bitmap.Height, Is.InRange(341, 342));
        Assert.That(bitmap.GetPixel(0, 0).Alpha, Is.Zero);
        Assert.That(bitmap.GetPixel(60, 180), Is.Not.EqualTo(bitmap.GetPixel(230, 180)), "The filled bar contains a gradient.");
        var preview = Path.Combine(TestContext.CurrentContext.WorkDirectory, "svg-preview.png");
        await File.WriteAllBytesAsync(preview, image.Bytes);
        TestContext.AddTestAttachment(preview);
    }
}
