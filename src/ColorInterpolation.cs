using System.Globalization;

namespace Ziopuzzle.CustomButton;

public static class ColorInterpolation
{
    public static double[] Decode(string rgb, string space)
    {
        var v = Enumerable.Range(0, 3).Select(i => byte.Parse(rgb.Substring(1 + i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d).ToArray();
        if (space == "linear-rgb") return v.Select(x => x <= .04045 ? x / 12.92 : Math.Pow((x + .055) / 1.055, 2.4)).ToArray();
        if (space != "hsv") return v;
        var max = v.Max(); var min = v.Min(); var delta = max - min;
        double hue = delta == 0 ? 0 : max == v[0] ? 60 * ((v[1] - v[2]) / delta % 6) : max == v[1] ? 60 * ((v[2] - v[0]) / delta + 2) : 60 * ((v[0] - v[1]) / delta + 4);
        return [(hue + 360) % 360, max == 0 ? 0 : delta / max, max];
    }
    public static string Encode(double[] v, string space)
    {
        if (space == "linear-rgb") v = v.Select(x => x <= .0031308 ? x * 12.92 : 1.055 * Math.Pow(x, 1 / 2.4) - .055).ToArray();
        else if (space == "hsv")
        {
            var h = (v[0] % 360 + 360) % 360 / 60;
            var c = v[2] * v[1]; var x = c * (1 - Math.Abs(h % 2 - 1)); var m = v[2] - c;
            double[] rgb = (int)h switch { 0 => [c, x, 0], 1 => [x, c, 0], 2 => [0, c, x], 3 => [0, x, c], 4 => [x, 0, c], _ => [c, 0, x] };
            v = rgb.Select(a => a + m).ToArray();
        }
        return "#" + string.Concat(v.Select(x => ((byte)Math.Round(Math.Clamp(x, 0, 1) * 255)).ToString("x2", CultureInfo.InvariantCulture)));
    }
}
