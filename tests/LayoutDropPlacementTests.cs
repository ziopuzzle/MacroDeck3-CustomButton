using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;

public class LayoutDropPlacementTests
{
    [TestCase(false, false, .4, "before")]
    [TestCase(false, false, .6, "after")]
    [TestCase(true, false, .1, "before")]
    [TestCase(true, false, .5, "inside")]
    [TestCase(true, false, .9, "after")]
    [TestCase(true, true, .1, "inside")]
    [TestCase(true, true, .9, "inside")]
    public void HeaderZones(bool container, bool root, double y, string expected)
        => Assert.That(LayoutDropPlacement.Header(container, root, y), Is.EqualTo(expected));

    [TestCase("stack")]
    [TestCase("layer")]
    public void MovingContainerPreservesChildrenAndSiblingOrder(string type)
    {
        var doc = new LayoutDocument($"<stack id='root'><{type} id='moving'><text id='child'>Keep</text></{type}><stack id='target'><text id='other'>Other</text></stack></stack>");
        doc.Place("moving", "target", LayoutDropPlacement.Header(true, false, .9));
        Assert.That(doc.Root.Elements().Select(n => (string?)n.Attribute("id")), Is.EqualTo(new[] { "target", "moving" }));
        Assert.That(doc.Find("child").Parent, Is.SameAs(doc.Find("moving")));
        Assert.Throws<FormatException>(() => doc.Place("moving", "child", "after"));
        doc.Place("moving", "target", LayoutDropPlacement.Header(true, false, .5));
        Assert.That(doc.Find("moving").Parent, Is.SameAs(doc.Find("target")));
    }
}

