using SkiaSharp;

namespace Ziopuzzle.CustomButton;

/// <summary>Alpha-weighted source colour, before client-side image effects.</summary>
public static class ImageAverageColor
{
    private static readonly object DecodeGate = new();
    public static string Calculate(byte[] bytes)
    {
        lock (DecodeGate) return CalculateCore(bytes);
    }
    private static string CalculateCore(byte[] bytes)
    {
        using var stream = new SKMemoryStream(bytes);
        using var codec = SKCodec.Create(stream) ?? throw new FormatException("Image color analysis could not decode the image.");
        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > 16_777_216)
            throw new FormatException("Image color analysis is limited to 16 megapixels.");
        using var bitmap = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw new FormatException("Image color analysis could not decode the first frame.");
        double r = 0, g = 0, b = 0, alpha = 0;
        // Area samples bound averaging work, including large images and narrow strips.
        var cols = Math.Min(128, info.Width); var rows = Math.Min(128, info.Height);
        for (var y = 0; y < rows; y++)
            for (var x = 0; x < cols; x++)
            {
                var pixel = bitmap.GetPixel((int)((x + .5) * info.Width / cols), (int)((y + .5) * info.Height / rows));
                alpha += pixel.Alpha;
                r += pixel.Red * pixel.Alpha; g += pixel.Green * pixel.Alpha; b += pixel.Blue * pixel.Alpha;
            }
        return alpha == 0 ? "#00000000" : $"#{(int)Math.Round(r / alpha):x2}{(int)Math.Round(g / alpha):x2}{(int)Math.Round(b / alpha):x2}";
    }
}
