using System.Text.Json;
using MacroDeck.Localization;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class LocalizationTests
{
    public static string Resolve(JsonElement value, string language = "en")
    {
        var registry = new LocalizationCatalogRegistry(); registry.Register(Strings.LocalizationCatalog);
        return new LocalizationResolver(registry).Resolve(value.Deserialize<LocalizedText>()!, language)!;
    }
    [TestCase("en")][TestCase("ja")][TestCase("de")]
    public void ProductTextIsEnglishRegardlessOfHostLanguage(string language)
    {
        var reference = JsonSerializer.SerializeToElement(TextCatalog.Reference("Drawing"));
        Assert.That(Resolve(reference, language), Is.EqualTo("Drawing"));
        Assert.That(TextCatalog.Language(language), Is.EqualTo("en"));
    }
    [Test] public void UserTextAndExpressionsAreNotTranslated()
    {
        const string content = "{{value}} / \u65e5\u672c\u8a9e";
        Assert.That(TextCatalog.Translate(content, "ja"), Is.EqualTo(content));
        Assert.That(Resolve(JsonSerializer.SerializeToElement(TextCatalog.Reference(content))), Is.EqualTo(content));
    }
}

