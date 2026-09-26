using System.Globalization;
using System.Text.RegularExpressions;

namespace Ziopuzzle.CustomButton;

/// <summary>XML uses CSS RGBA order; the host receives RGB plus a separate opacity.</summary>
public readonly record struct DisplayColor(string Rgb, byte Alpha)
{
    public double Opacity => Alpha / 255d;
    public string Hex => Alpha == 255 ? Rgb : Rgb + Alpha.ToString("x2", CultureInfo.InvariantCulture);
    public static DisplayColor Parse(string value)
    {
        if (value == "transparent") return new("#000000", 0);
        if (!Regex.IsMatch(value, "^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$"))
            throw new FormatException("Use #RGB, #RGBA, #RRGGBB or #RRGGBBAA colors.");
        if (value.Length is 4 or 5) value = "#" + string.Concat(value[1..].Select(c => new string(c, 2)));
        return new(value[..7].ToLowerInvariant(), value.Length == 9 ? byte.Parse(value[7..], NumberStyles.HexNumber, CultureInfo.InvariantCulture) : (byte)255);
    }
}
