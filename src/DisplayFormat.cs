using System.Globalization;
using System.Text.RegularExpressions;

namespace Ziopuzzle.CustomButton;

/// <summary>Explicit invariant display formats; durations always consume seconds.</summary>
public static class DisplayFormat
{
    public static string Apply(string raw, string format)
    {
        format = format.Trim();
        bool duration = format is "duration" or "duration:mm:ss" or "duration:hh:mm:ss";
        if (!duration && !Regex.IsMatch(format, @"\A(0{1,8}(\.0{1,6})?|[FfNnPp][0-6]?)\z", RegexOptions.CultureInvariant))
            throw new FormatException("Use 0, 00, 0.00, F2, N1, P0, duration, duration:mm:ss or duration:hh:mm:ss.");
        if (raw == "—") return raw;
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
        {
            if (duration) throw new FormatException("Duration formatting requires a finite number of seconds.");
            return raw;
        }
        if (!duration) return number.ToString(format, CultureInfo.InvariantCulture);
        // A bounded integer range avoids overflow and keeps second decomposition exact.
        if (Math.Abs(number) > 9_007_199_254_740_991d)
            throw new FormatException("Duration seconds exceed the supported range.");
        var seconds = (long)Math.Floor(Math.Abs(number));
        var sign = number < 0 && seconds > 0 ? "-" : "";
        bool hours = format == "duration:hh:mm:ss" || format == "duration" && seconds >= 3600;
        return hours
            ? string.Create(CultureInfo.InvariantCulture, $"{sign}{seconds / 3600}:{seconds / 60 % 60:00}:{seconds % 60:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{sign}{seconds / 60}:{seconds % 60:00}");
    }
}
