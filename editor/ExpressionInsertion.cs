using System.Text.RegularExpressions;

namespace Ziopuzzle.CustomButton;

public sealed record ExpressionChoice(string Group, string Label, string Description, string First, string Tail = "", string? Function = null, bool Condition = false)
{
    public override string ToString() => Group + " · " + Label;
    public string Example => Function == null ? First : Function + "(" + First + Tail + ")";
}

public static class ExpressionInsertion
{
    public sealed record Result(string Text, int Start, int End);
    public static readonly ExpressionChoice[] Functions =
    [
        new("Numbers", "floor(x)", "Round down to an integer.", "0", Function: "floor"),
        new("Numbers", "ceil(x)", "Round up to an integer.", "0", Function: "ceil"),
        new("Numbers", "trunc(x)", "Discard the fractional part toward zero.", "0", Function: "trunc"),
        new("Numbers", "round(x, digits)", "Round to 0–6 decimal places. The digits argument can be omitted.", "0", ", 0", "round"),
        new("Numbers", "abs(x)", "Absolute value.", "0", Function: "abs"),
        new("Numbers", "min(a, b)", "The smaller of two numbers.", "0", ", 1", "min"),
        new("Numbers", "max(a, b)", "The larger of two numbers.", "0", ", 1", "max"),
        new("Numbers", "clamp(x, low, high)", "Limit a number to the given range.", "0", ", 0, 1", "clamp"),
        new("Numbers", "lerp(a, b, t)", "a + (b-a)*t. Ratio t is not clamped.", "0", ", 1, 0.5", "lerp"),
        new("Numbers", "sqrt(x)", "Square root of a nonnegative number.", "0", Function: "sqrt"),
        new("Numbers", "pow(x, y)", "Raise x to the power y.", "1", ", 2", "pow"),
        new("Numbers", "sin(x)", "Sine; angle is in radians.", "0", Function: "sin"),
        new("Numbers", "cos(x)", "Cosine; angle is in radians.", "0", Function: "cos"),
        new("Data", "number(key, fallback)", "Read a finite number or numeric string; otherwise use fallback. First argument must be a data name.", "value", ", 0", "number", true),
        new("Data", "isNumber(key)", "Test the JSON number type. Returns 1/0 in calculations and true/false in conditions.", "value", Function: "isNumber", Condition: true),
        new("Data", "isNumeric(key)", "Test whether data can be converted to a finite number.", "value", Function: "isNumeric", Condition: true),
        new("Conditions", "type(key)", "Return number, string, boolean, null or missing. Conditions only; compare with a quoted type name.", "value", Function: "type", Condition: true),
        new("Colors", "mixColor(a, b, t, space)", "Mix colors with t from 0 to 1. Spaces: 'linear-rgb' (default), 'srgb', 'hsv' (shortest), 'hsv+' (increasing hue), 'hsv-' (decreasing hue). Alpha-aware.", "'#ffffff'", ", '#000000', 0.5, 'linear-rgb'", "mixColor"),
        new("Colors", "shiftHue(color, degrees)", "Rotate HSV hue in degrees; wraps at 360.", "'#ffffff'", ", 30", "shiftHue"),
        new("Colors", "setSaturation(color, value)", "Set HSV saturation from 0 to 1; preserve alpha.", "'#ffffff'", ", 0.5", "setSaturation"),
        new("Colors", "scaleSaturation(color, factor)", "Multiply HSV saturation, clamping the result to 0–1.", "'#ffffff'", ", 0.5", "scaleSaturation"),
        new("Colors", "setBrightness(color, value)", "Set HSV value from 0 to 1, not perceived luminance.", "'#ffffff'", ", 0.5", "setBrightness"),
        new("Colors", "scaleBrightness(color, factor)", "Multiply HSV value, clamping the result to 0–1.", "'#ffffff'", ", 0.5", "scaleBrightness"),
        new("Colors", "setAlpha(color, alpha)", "Replace alpha: 0 transparent, 1 opaque. Alpha is the last hex pair.", "'#ffffff'", ", 0.5", "setAlpha")
    ];
    public static bool KeyOnly(string attribute) => attribute is "key" or "keyX" or "keyY" or "items";
    public static bool IsCondition(string attribute) => attribute is "when" or "visibleWhen";
    public static bool Available(string attribute) => attribute is not ("id" or "itemKey" or "averageColorKey" or "averageColorFallback");

    public static Result Insert(string text, int start, int end, string attribute, ExpressionChoice choice)
    {
        (start, end) = (Math.Min(start, end), Math.Max(start, end));
        if (start < 0 || end > text.Length) throw new FormatException("The insertion position is no longer valid.");
        var condition = IsCondition(attribute);
        if (choice.Function != null && (KeyOnly(attribute) || condition && !choice.Condition || !condition && choice.Function == "type"))
            throw new FormatException("This function is not supported in this field.");
        var prefix = ""; var suffix = ""; var inCalculation = false;
        var first = choice.First;
        if (!condition && !KeyOnly(attribute))
        {
            // Treat a selected complete binding as its contents; never nest template delimiters.
            var selected = text[start..end];
            if (selected.StartsWith("{{") && selected.EndsWith("}}") && selected.IndexOf("}}", StringComparison.Ordinal) == selected.Length - 2)
            { start += 2; end -= 2; }
            if (start > 0 && start < text.Length && text[start - 1] == '{' && text[start] == '{')
                throw new FormatException("Place the cursor inside or outside the binding, not between its braces.");
            var open = text.Length == 0 ? -1 : text.LastIndexOf("{{", Math.Max(0, start - 1), StringComparison.Ordinal);
            var lastClose = text.Length == 0 ? -1 : text.LastIndexOf("}}", Math.Max(0, start - 1), StringComparison.Ordinal);
            if (open >= 0 && open > lastClose && start >= open + 2)
            {
                var close = text.IndexOf("}}", open + 2, StringComparison.Ordinal);
                if (close < 0 || end > close) throw new FormatException("Complete the {{...}} binding and select within one binding before inserting.");
                var bodyStart = open + 2;
                while (bodyStart < close && char.IsWhiteSpace(text[bodyStart])) bodyStart++;
                var expression = bodyStart < close && text[bodyStart] == '=';
                inCalculation = expression;
                var contentStart = expression ? bodyStart + 1 : bodyStart;
                var colon = -1; char quote = '\0';
                for (var i = contentStart; i < close; i++)
                {
                    if (i == start && quote != '\0') throw new FormatException("Select the complete quoted argument, or place the cursor outside the quoted string.");
                    var c = text[i];
                    if (quote != '\0') { if (c == '\\') i++; else if (c == quote) quote = '\0'; }
                    else if (c is '\'' or '"') quote = c;
                    else if (c == ':') { colon = i; break; }
                }
                if (colon >= 0 && (start > colon || end > colon)) throw new FormatException("Insert before the format suffix, not inside it.");
                if (!expression)
                {
                    // A direct binding has one data name. Replace it, or wrap it in a function.
                    start = bodyStart; end = colon >= 0 ? colon : close;
                    if (choice.Function != null)
                    {
                        first = text[start..end].Trim(); if (first.Length == 0) first = choice.First;
                        if (first.Contains('-')) throw new FormatException("A hyphenated data name cannot be wrapped in a calculation. Rename the key first.");
                        prefix = "= ";
                    }
                }
                else
                {
                    start = Math.Max(start, contentStart);
                    end = Math.Max(end, start);
                    if (choice.Function != null && end > start) first = text[start..end].Trim();
                }
            }
            else
            {
                if (text[start..end].Contains("{{") || text[start..end].Contains("}}"))
                    throw new FormatException("Select one complete binding or text outside bindings.");
                prefix = choice.Function == null ? "{{" : "{{= "; suffix = "}}";
                if (choice.Function != null && end > start) first = text[start..end].Trim();
            }
        }
        else if (choice.Function != null && end > start) first = text[start..end].Trim();
        if (choice.Function == null)
        {
            // Hyphens are valid direct keys but mean subtraction inside calculations.
            if (inCalculation && !Regex.IsMatch(first, @"^[A-Za-z_][A-Za-z0-9_.]*$"))
                throw new FormatException("This data name cannot be used in a calculation. Use a direct binding or rename the key.");
        }
        var inserted = prefix + (choice.Function == null ? first : choice.Function + "(" + first + choice.Tail + ")") + suffix;
        var result = text[..start] + inserted + text[end..];
        var cursor = start + prefix.Length + (choice.Function == null ? first.Length : choice.Function.Length + 1);
        return new(result, cursor, choice.Function == null ? cursor : cursor + first.Length);
    }
}
