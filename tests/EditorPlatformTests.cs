using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;
public class EditorPlatformTests
{
    [TestCase(true, "Ziopuzzle.CustomButton.Editor.exe")]
    [TestCase(false, "Ziopuzzle.CustomButton.Editor")]
    public void LaunchesPlatformSpecificAppHost(bool windows, string expected)
        => Assert.That(NativeEditorSession.EditorExecutablePath("package", windows), Is.EqualTo(Path.Combine("package", expected)));
}

