using MacroDeck.Localization;

namespace Ziopuzzle.CustomButton;

/// <summary>English-only product text. User-provided content is returned unchanged.</summary>
public static class TextCatalog
{
    public static LocalizedText Reference(string value) => LocalizedText.FromLiteral(value);
    public static string Language(string? requested) => "en";
    public static string Translate(string value, string language) => value;
}
