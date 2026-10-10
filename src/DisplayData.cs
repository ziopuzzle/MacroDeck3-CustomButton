using System.Globalization;
using System.Text.Json;

namespace Ziopuzzle.CustomButton;

public static class DisplayData
{
    public static string TypeOf(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Undefined => "missing", JsonValueKind.Null => "null",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        _ => value.ValueKind.ToString().ToLowerInvariant()
    };
    public static bool TryNumber(JsonElement value, out double number)
    {
        number = 0;
        return value.ValueKind is JsonValueKind.Number or JsonValueKind.String
            && double.TryParse(value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText(),
                NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number);
    }
}
