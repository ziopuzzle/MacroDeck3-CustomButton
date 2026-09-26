using System.Text.Json;
using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;
public class PathTests
{
    [TestCase(180, 180)][TestCase(0, -180)][TestCase(-180, 180)]
    public void UpperSectorCanUseBottomEdgeAsCenter(double start, double sweep)
    {
        Assert.DoesNotThrow(() => ShapePaths.Sector(.5, 1, .5, start, sweep));
    }
    [TestCase(0, 180)][TestCase(180, -180)][TestCase(0, 360)]
    public void VisibleSectorOutsideCanvasStillFails(double start, double sweep)
    {
        Assert.Throws<FormatException>(() => ShapePaths.Sector(.5, 1, .5, start, sweep));
    }
    [Test] public void PolygonPreservesCoordinatesWithoutScanConversion()
        => Assert.That(ShapePaths.Polygon("10%,90%;50%,10%;90%,90%"), Is.EqualTo("M0.1 0.9 L0.5 0.1 L0.9 0.9 Z"));
    [TestCase(90, "M0.5 0.5 L0.9 0.5 A0.4 0.4 0 0 1 0.5 0.9 Z")]
    [TestCase(-90, "M0.5 0.5 L0.9 0.5 A0.4 0.4 0 0 0 0.5 0.1 Z")]
    [TestCase(360, "M0.9 0.5 A0.4 0.4 0 0 1 0.1 0.5 A0.4 0.4 0 0 1 0.9 0.5 Z")]
    public void SectorUsesExactSvgArcs(double sweep, string expected) => Assert.That(ShapePaths.Sector(.5, .5, .4, 0, sweep), Is.EqualTo(expected));
    [TestCase("M0 0 C0 1 1 0 1 1 Q0.5 1 0 0 Z")][TestCase("M0,0 H1 V.5 L1e-1 0 Z")]
    public void SupportedAbsolutePathsAreAccepted(string path) => Assert.That(ShapePaths.Validate(path), Is.EqualTo(path));
    [TestCase("m0 0 l1 1")][TestCase("M0 0 L1")][TestCase("M0 0 A1 1 0 2 0 1 1")][TestCase("M1e999 0")][TestCase("<svg/>")]
    public void InvalidPathsAreExplainedBeforeSendingToHost(string path) => Assert.Throws<FormatException>(() => ShapePaths.Validate(path));
    [TestCase("1%,2%;3%,4%")][TestCase("0%,0%;200%,50%;50%,50%")]
    public void InvalidPointsAreRejected(string points) => Assert.Throws<FormatException>(() => ShapePaths.ParsePoints(points));
    [Test] public async Task ManyShapesStaySmallAndUseNativeShapeNodes()
    {
        var xml = "<layer id='canvas'>" + string.Concat(Enumerable.Range(0, 16).Select(i => $"<sector id='s{i}' sweepAngle='360'/>")) + "</layer>";
        await using var session = new ButtonSession(ButtonTests.Surface(), TestLayouts.Sample with { Layout = xml }, null);
        var tree = session.BuildTree();
        Assert.That(ButtonTests.Nodes(tree.Root).Count(n => n.Type == "ui.shape"), Is.EqualTo(16));
        Assert.That(JsonSerializer.SerializeToUtf8Bytes(tree).Length, Is.LessThan(16000));
    }
}

