using System.Globalization;
using System.Text.RegularExpressions;

namespace Ziopuzzle.CustomButton;

/// <summary>Maps geometry into the host path component's normalized local box.</summary>
public static class LocalShapePath
{
    private static string F(double n) => n.ToString("0.########", CultureInfo.InvariantCulture);
    public static string Build(string type, double x, double y, double width, double height, double radius, string data)
    {
        if (x + width > 1 + 1e-9 || y + height > 1 + 1e-9)
            throw new FormatException("Local shapes must fit within 0–100%: x + width and y + height must not exceed 100%.");
        if (type == "path") return Transform(ShapePaths.Validate(data), x, y, width, height);
        if (type == "circle") return $"M{F(x)} {F(y + height / 2)} A{F(width / 2)} {F(height / 2)} 0 1 1 {F(x + width)} {F(y + height / 2)} A{F(width / 2)} {F(height / 2)} 0 1 1 {F(x)} {F(y + height / 2)} Z";
        var r = type == "capsule" ? Math.Min(width, height) / 2 : Math.Min(radius, Math.Min(width, height) / 2);
        if (r == 0) return $"M{F(x)} {F(y)} H{F(x + width)} V{F(y + height)} H{F(x)} Z";
        return $"M{F(x + r)} {F(y)} H{F(x + width - r)} A{F(r)} {F(r)} 0 0 1 {F(x + width)} {F(y + r)} V{F(y + height - r)} A{F(r)} {F(r)} 0 0 1 {F(x + width - r)} {F(y + height)} H{F(x + r)} A{F(r)} {F(r)} 0 0 1 {F(x)} {F(y + height - r)} V{F(y + r)} A{F(r)} {F(r)} 0 0 1 {F(x + r)} {F(y)} Z";
    }
    private static string Transform(string path, double x, double y, double width, double height)
    {
        char command = 'M'; int index = 0;
        return Regex.Replace(path, @"[MLHVCQAZ]|[+-]?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?", match =>
        {
            if (match.Length == 1 && char.IsUpper(match.Value[0])) { command = match.Value[0]; index = 0; return match.Value; }
            var n = double.Parse(match.Value, CultureInfo.InvariantCulture);
            var i = index++;
            if (command == 'A')
            {
                // An axis-aligned source ellipse remains axis aligned under independent X/Y scaling.
                i %= 7;
                if (i == 2 && n % 180 != 0 && width != height) throw new FormatException("Local paths with rotated arcs need equal width and height.");
                return F(i switch { 0 => n * width, 1 => n * height, 5 => x + n * width, 6 => y + n * height, _ => n });
            }
            return F(command == 'H' ? x + n * width : command == 'V' ? y + n * height : i % 2 == 0 ? x + n * width : y + n * height);
        });
    }
}
