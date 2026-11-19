using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class RendererAttributeDiagnosticsTests
{
    [TestCase("color", "invalid")]
    [TestCase("size", "invalid")]
    [TestCase("size", "101%")]
    [TestCase("opacity", "2")]
    [TestCase("wrap", "invalid")]
    [TestCase("weight", "invalid")]
    [TestCase("maxLines", "1.5")]
    [TestCase("transitionMs", "-1")]
    [TestCase("easing", "invalid")]
    [TestCase("colorSpace", "invalid")]
    [TestCase("transitionProperties", "invalid")]
    public void ScalarErrorsIdentifyTheRawAttribute(string name, string value)
    {
        var error = Error($"<text id='title' {name}='{value}'>{{{{title}}}}</text>");
        Assert.That(error.Data["LayoutAttribute"], Is.EqualTo(name));
        Assert.That(error.Data["LayoutAttributeValue"], Is.EqualTo(value));
    }

    [TestCase("{{tint}}", "invalid")]
    [TestCase("invalid", "{{tint}}")]
    public void ConditionalStylesReportTheEffectiveSource(string baseColor, string styleColor)
    {
        var error = Error($"<text id='title' color='{baseColor}'><style when='active' color='{styleColor}'/>Title</text>",
            """{"active":true,"tint":"invalid"}""");
        Assert.That(error.Data["LayoutAttribute"], Is.EqualTo("color"));
        Assert.That(error.Data["LayoutAttributeValue"], Is.EqualTo(styleColor));
        Assert.That(error.Message, Is.EqualTo("<text> (ID 'title'): Use #RGB, #RGBA, #RRGGBB or #RRGGBBAA colors. (line 1, column 2)"));
    }

    [TestCase("invalid")]
    [TestCase("{{item.tint}}")]
    public void RepeatedChildErrorsKeepTheChildLocationAndLocallySelectedStyle(string styleColor)
    {
        var xml = "<repeat id='rows' items='rows'>\n  <layer id='row'>\n    <text id='title' color='#fff'><style when='item.active' color='" + styleColor + "'/>{{item.title}}</text>\n  </layer>\n</repeat>";
        var error = Error(xml, """{"rows":[{"active":false},{"active":true,"tint":"invalid"}]}""");
        Assert.That(error.Data["LayoutAttribute"], Is.EqualTo("color"));
        Assert.That(error.Data["LayoutAttributeValue"], Is.EqualTo(styleColor));
        Assert.That(error.Data["LayoutLine"], Is.EqualTo(3));
        Assert.That(error.Message, Does.Contain("ID 'title'").And.Not.Contain("ID 'rows'").And.Not.Contain("ID 'row'"));
    }

    [Test]
    public void ABindingExpressionErrorIdentifiesTheAttributeThatEvaluatedIt()
    {
        const string expression = "{{= mixColor(tint, '#000', 0.5)}}";
        var error = Error("<text id='title' color=\"" + expression + "\">Title</text>", """{"tint":"invalid"}""");
        Assert.That(error.Data["LayoutAttribute"], Is.EqualTo("color"));
        Assert.That(error.Data["LayoutAttributeValue"], Is.EqualTo(expression));
    }

    [TestCase("<trackpad id='node' color='invalid' opacity='{{alpha}}'/>", "color", "invalid", "Use #RGB, #RGBA, #RRGGBB or #RRGGBBAA colors.")]
    [TestCase("<trackpad id='node' background='invalid' opacity='{{alpha}}'/>", "background", "invalid", "Use #RGB, #RGBA, #RRGGBB or #RRGGBBAA colors.")]
    [TestCase("<chart id='node' key='history' points='2.5' opacity='{{alpha}}'/>", "points", "2.5", "For chart, points must be an integer.")]
    [TestCase("<progress-bar id='node' positionMs='1.5' opacity='{{alpha}}'/>", "positionMs", "1.5", "positionMs must be an integer number of milliseconds.")]
    [TestCase("<progress-bar id='node' durationMs='1.5' opacity='{{alpha}}'/>", "durationMs", "1.5", "durationMs must be an integer number of milliseconds.")]
    [TestCase("<progress-bar id='node' anchor='invalid' opacity='{{alpha}}'/>", "anchor", "invalid", "anchor must be an ISO timestamp with a time zone, e.g. 2026-09-13T12:00:00+09:00.")]
    public void ComponentScalarErrorsIdentifyTheirSourceWithoutChangingTheMessage(string xml, string name, string raw, string message)
    {
        var error = Error(xml);
        Assert.That(error.Data["LayoutAttribute"], Is.EqualTo(name));
        Assert.That(error.Data["LayoutAttributeValue"], Is.EqualTo(raw));
        Assert.That(error.InnerException!.Message, Is.EqualTo(message));
    }

    [TestCase("{{width}}", "invalid")]
    [TestCase("invalid", "{{width}}")]
    public void ResponsiveBoundsReportTheVariantAndItsEffectiveStyleSource(string baseWidth, string styleWidth)
    {
        var xml = "<responsive id='layout'>\n  <variant id='compact'/>\n  <variant id='wide' minWidth='" + baseWidth + "'><style when='active' minWidth='" + styleWidth + "'/></variant>\n</responsive>";
        var error = Error(xml, """{"active":true,"width":"invalid"}""");
        Assert.That(error.Data["LayoutAttribute"], Is.EqualTo("minWidth"));
        Assert.That(error.Data["LayoutAttributeValue"], Is.EqualTo(styleWidth));
        Assert.That(error.Data["LayoutLine"], Is.EqualTo(3));
        Assert.That(error.Message, Does.StartWith("<variant> (ID 'wide'): minWidth needs a nonnegative cell count or positive aspect ratio."));
    }

    [TestCase("invalid")]
    [TestCase("{{item.width}}")]
    public void RepeatedResponsiveBoundsUseTheCurrentItemsStyle(string width)
    {
        var xml = "<repeat id='rows' items='rows'>\n  <responsive id='layout'><variant id='compact'/>\n    <variant id='wide' minWidth='2'><style when='item.active' minWidth='" + width + "'/></variant>\n  </responsive>\n</repeat>";
        var error = Error(xml, """{"rows":[{"active":false},{"active":true,"width":"invalid"}]}""");
        Assert.That(error.Data["LayoutAttribute"], Is.EqualTo("minWidth"));
        Assert.That(error.Data["LayoutAttributeValue"], Is.EqualTo(width));
        Assert.That(error.Data["LayoutLine"], Is.EqualTo(3));
        Assert.That(error.Message, Does.Contain("ID 'wide'").And.Not.Contain("ID 'layout'").And.Not.Contain("ID 'rows'"));
    }

    [Test]
    public void ResponsiveRangeRelationshipsStayUntaggedButBelongToTheirVariant()
    {
        var error = Error("<responsive id='layout'><variant id='compact'/><variant id='wide' minWidth='5' maxWidth='{{width}}'/></responsive>", """{"width":2}""");
        Assert.That(error.Data.Contains("LayoutAttribute"), Is.False);
        Assert.That(error.Message, Does.StartWith("<variant> (ID 'wide'): Responsive maximum bounds must exceed minimum bounds."));
    }

    [Test]
    public void StepRangeErrorsRemainDependentOnTheOtherRangeAttributes()
    {
        var error = Error("<slider id='level' max='{{limit}}' step='2'/>", """{"limit":1}""");
        Assert.That(error.Message, Does.Contain("step must be a number between 0 and 1"));
        Assert.That(error.Data.Contains("LayoutAttribute"), Is.False);
        Assert.DoesNotThrow(() => new LayoutRenderer("<slider id='level' max='{{limit}}' step='2'/>").Render(DataHub.ParseValues("""{"limit":10}""")));
    }

    private static FormatException Error(string xml, string data = "{}") =>
        Assert.Throws<FormatException>(() => new LayoutRenderer(xml).Render(DataHub.ParseValues(data)))!;
}
