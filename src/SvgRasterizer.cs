using System.Globalization;
using System.Text;
using SkiaSharp;
using Svg.Skia;

namespace Ziopuzzle.CustomButton;

public static class SvgRasterizer
{
    // Bound concurrent native rendering work across all button sessions.
    private static readonly SemaphoreSlim RenderGate = new(1, 1);
    public static async Task<ImageSourceLoader.Image> RenderAsync(string source, int maxBytes, CancellationToken ct)
    {
        var separator = source.IndexOf('\n');
        if (separator < 0 || !int.TryParse(source[SvgTemplate.Prefix.Length..separator], NumberStyles.None, CultureInfo.InvariantCulture, out var size) || size is < 64 or > 1024)
            throw new FormatException("SVG rasterSize must be between 64 and 1024 pixels.");
        var xml = SvgTemplate.Parse(source[(separator + 1)..]).ToString();
        await RenderGate.WaitAsync(ct);
        try
        {
            ct.ThrowIfCancellationRequested();
            using var svg = new SKSvg();
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
            var picture = svg.Load(stream) ?? throw new FormatException("SVG could not be rendered.");
            var bounds = picture.CullRect;
            if (!float.IsFinite(bounds.Width) || !float.IsFinite(bounds.Height) || bounds.Width <= 0 || bounds.Height <= 0 || bounds.Width > 100000 || bounds.Height > 100000)
                throw new FormatException("SVG needs finite, positive width and height or a viewBox.");
            var scale = size / Math.Max(bounds.Width, bounds.Height);
            var width = Math.Max(1, (int)Math.Ceiling(bounds.Width * scale));
            var height = Math.Max(1, (int)Math.Ceiling(bounds.Height * scale));
            using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul)) ?? throw new FormatException("SVG rendering surface could not be created.");
            surface.Canvas.Clear(SKColors.Transparent);
            surface.Canvas.Scale(scale);
            surface.Canvas.Translate(-bounds.Left, -bounds.Top);
            surface.Canvas.DrawPicture(picture);
            ct.ThrowIfCancellationRequested();
            using var snapshot = surface.Snapshot();
            using var png = snapshot.Encode(SKEncodedImageFormat.Png, 100);
            if (png.Size > maxBytes) throw new FormatException("Rendered SVG exceeds the image byte limit.");
            return new(png.ToArray(), "image/png");
        }
        finally { RenderGate.Release(); }
    }
}
