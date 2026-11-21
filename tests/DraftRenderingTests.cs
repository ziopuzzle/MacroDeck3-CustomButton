using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class DraftRenderingTests
{
    // Each component kind is a descendant of a following list. Its first live
    // frame intentionally contains no rows, while draft validation needs every row.
    [TestCase("repeat", "direction", " items='rows'")]
    [TestCase("scroll", "gap")]
    [TestCase("stack", "align")]
    [TestCase("layer", "background")]
    [TestCase("transform", "rotation")]
    [TestCase("modifier", "height")]
    [TestCase("responsive", "opacity")]
    [TestCase("variant", "minAspect")]
    [TestCase("dynamic-text", "format")]
    [TestCase("icon", "name")]
    [TestCase("gauge", "startAngle")]
    [TestCase("image", "zoom")]
    [TestCase("svg", "rasterSize")]
    [TestCase("text", "weight")]
    [TestCase("bar", "thickness")]
    [TestCase("chart", "points", " key='series'")]
    [TestCase("clock", "seconds")]
    [TestCase("progress-bar", "rate")]
    [TestCase("slider", "min")]
    [TestCase("dial", "endAngle")]
    [TestCase("trackpad", "leftValue")]
    [TestCase("text-field", "size")]
    [TestCase("toggle", "color")]
    [TestCase("segmented", "color", "", "<text id='choice'>A</text>")]
    [TestCase("rect", "width")]
    [TestCase("circle", "x")]
    [TestCase("capsule", "y")]
    [TestCase("path", "strokeWidth")]
    [TestCase("line", "thickness")]
    [TestCase("polygon", "strokeWidth")]
    [TestCase("sector", "sweepAngle")]
    public void FollowingListChecksComponentProperties(string type, string field, string attributes = "", string body = "")
    {
        var component = $"<{type} id='target' {field}='abc'{attributes}>{body}</{type}>";
        if (type == "variant") component = $"<responsive id='layout'><variant id='default'/>{component}</responsive>";
        var xml = $"<scroll id='s' followKey='selected'><repeat id='rowsList' items='rows'>{component}</repeat></scroll>";
        var data = DataHub.ParseValues("""{"rows":[1],"selected":0}""");
        Assert.DoesNotThrow(() => new LayoutRenderer(xml).Render(data));
        var error = Assert.Throws<FormatException>(() => new LayoutRenderer(xml).ValidateDraft(data))!;
        Assert.That(error.Data["LayoutId"], Is.EqualTo("target"));
        Assert.That(error.Data["LayoutField"], Is.EqualTo(field));
    }

    [TestCase("height", "401%")]
    [TestCase("mainSize", "abc")]
    [TestCase("fill", "abc")]
    public void EmptyRepeatChecksModifierLayout(string field, string value)
    {
        var xml = $"<repeat id='r' items='rows'><modifier id='frame' {field}='{value}'/></repeat>";
        var error = Assert.Throws<FormatException>(() => new LayoutRenderer(xml).ValidateDraft(DataHub.ParseValues("{}")))!;
        Assert.That(error.Data["LayoutField"], Is.EqualTo(field));
    }

    [Test]
    public void HiddenComponentChecksLiteralFieldsAndDefersUnresolvedBindings()
    {
        var data = DataHub.ParseValues("{}");
        void Check(string min) => new LayoutRenderer($"<stack id='s' visible='false'><slider id='level' min='{min}'/></stack>").ValidateDraft(data);
        Assert.That(Assert.Throws<FormatException>(() => Check("0a"))!.Data["LayoutField"], Is.EqualTo("min"));
        Assert.DoesNotThrow(() => Check("{{value}}"));
    }

    [Test]
    public void EarlierBindingFailureDoesNotHideALiteralErrorInAnotherComponent()
    {
        var renderer = new LayoutRenderer("<stack id='s'><text id='dynamic' color='{{missing}}'/><slider id='level' min='0a'/></stack>");
        Assert.That(Assert.Throws<FormatException>(() => renderer.ValidateDraft(DataHub.ParseValues("{}")))!.Data["LayoutId"], Is.EqualTo("level"));
    }

    [Test]
    public void CompletedDraftChecksTheSerializedTreeBudget()
    {
        var renderer = new LayoutRenderer($"<text id='label'>{new string('a', LayoutLimits.RenderBytes)}</text>");
        var data = DataHub.ParseValues("{}");
        Assert.DoesNotThrow(() => renderer.Render(data));
        Assert.That(Assert.Throws<FormatException>(() => renderer.ValidateDraft(data))!.Message, Does.Contain("bytes"));
    }

    [Test]
    public void ModalEditorChecksCloseActionsInTheirActualSurfaceContext()
    {
        var renderer = new LayoutRenderer($"<layer id='close' closeModal='true' result='{new string('x', 4097)}'/>");
        var data = DataHub.ParseValues("{}");
        Assert.DoesNotThrow(() => renderer.ValidateDraft(data));
        Assert.That(Assert.Throws<FormatException>(() => renderer.ValidateDraft(data, modal: true))!.Data["LayoutId"], Is.EqualTo("close"));
    }
}
