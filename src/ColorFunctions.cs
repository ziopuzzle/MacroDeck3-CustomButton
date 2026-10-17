using System.Globalization;

namespace Ziopuzzle.CustomButton;

/// <summary>Colour transforms shared by the renderer and GUI; alpha is always the last hex pair.</summary>
public static class ColorFunctions
{
    public static bool IsSupported(string name) => name is "mixColor" or "shiftHue" or "setSaturation" or "scaleSaturation"
        or "setBrightness" or "scaleBrightness" or "setAlpha";
    public static void ValidateCount(string name, int count)
    {
        if (!IsSupported(name) || (name == "mixColor" ? count is not (3 or 4) : count != 2))
            throw new FormatException($"Unknown function or incorrect argument count: {name}.");
    }
    public static string Evaluate(string name, object[] args)
    {
        ValidateCount(name, args.Length);
        DisplayColor Color(int i) => args[i] is string color ? DisplayColor.Parse(color)
            : throw new FormatException($"{name} argument {i + 1} must be a color string.");
        double Number(int i)
        {
            var n = args[i] is double value ? value
                : args[i] is string text && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : double.NaN;
            return double.IsFinite(n) ? n : throw new FormatException($"{name} argument {i + 1} must be a finite number.");
        }
        var first = Color(0);
        if (name == "mixColor")
        {
            var second = Color(1); var t = Math.Clamp(Number(2), 0, 1);
            var space = args.Length == 3 ? "linear-rgb" : args[3] as string;
            space = space switch { "hsv+" => "hsv-increasing", "hsv-" => "hsv-decreasing", _ => space };
            if (space is not ("linear-rgb" or "srgb" or "hsv" or "hsv-increasing" or "hsv-decreasing"))
                throw new FormatException("mixColor space must be linear-rgb, srgb, hsv, hsv-increasing (hsv+) or hsv-decreasing (hsv-).");
            if (t == 0) return first.Hex;
            if (t == 1) return second.Hex;
            var alpha = first.Opacity * (1 - t) + second.Opacity * t;
            if (alpha == 0) return "#00000000";
            // Interpolate premultiplied colours: transparent black must not darken a visible endpoint.
            var weight = second.Opacity * t / alpha;
            var hsv = space.StartsWith("hsv", StringComparison.Ordinal);
            var encoding = hsv ? "hsv" : space;
            var a = ColorInterpolation.Decode(first.Rgb, encoding);
            var b = ColorInterpolation.Decode(second.Rgb, encoding);
            if (hsv)
            {
                if (a[1] == 0) a[0] = b[0];
                if (b[1] == 0) b[0] = a[0];
                var increasing = (b[0] - a[0] + 360) % 360;
                var delta = space switch
                {
                    "hsv-increasing" => increasing,
                    "hsv-decreasing" => increasing == 0 ? 0 : increasing - 360,
                    _ => increasing <= 180 ? increasing : increasing - 360
                };
                b[0] = a[0] + delta;
            }
            return new DisplayColor(ColorInterpolation.Encode(a.Select((v, i) => v + (b[i] - v) * weight).ToArray(), encoding), Alpha(alpha)).Hex;
        }
        var number = Number(1);
        if (name == "setAlpha") return new DisplayColor(first.Rgb, Alpha(number)).Hex;
        var components = ColorInterpolation.Decode(first.Rgb, "hsv");
        switch (name)
        {
            case "shiftHue": components[0] = (components[0] + number % 360 + 360) % 360; break;
            case "setSaturation": components[1] = Math.Clamp(number, 0, 1); break;
            case "scaleSaturation": components[1] = Math.Clamp(components[1] * number, 0, 1); break;
            case "setBrightness": components[2] = Math.Clamp(number, 0, 1); break;
            case "scaleBrightness": components[2] = Math.Clamp(components[2] * number, 0, 1); break;
        }
        return new DisplayColor(ColorInterpolation.Encode(components, "hsv"), first.Alpha).Hex;
    }
    private static byte Alpha(double value) => (byte)Math.Round(Math.Clamp(value, 0, 1) * 255);
}
