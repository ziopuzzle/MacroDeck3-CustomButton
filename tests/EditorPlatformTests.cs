using NUnit.Framework;

namespace Ziopuzzle.CustomButton.Tests;
public class EditorPlatformTests
{
    [Test] public void EditorUsesSelectedRuntimeAndPreservesPathsWithSpaces()
    {
        var host = Path.Combine("runtime folder", "dotnet.exe");
        var info = NativeEditorSession.EditorStartInfo("package folder", host);
        Assert.That(info.FileName, Is.EqualTo(host));
        Assert.That(info.ArgumentList, Is.EqualTo(new[] { Path.Combine("package folder", "Ziopuzzle.CustomButton.Editor.dll") }));
        Assert.That(info.UseShellExecute, Is.False);
        Assert.That(info.RedirectStandardInput && info.RedirectStandardOutput, Is.True);
    }
    [TestCase("/runtime/dotnet", "/other/dotnet", "/runtime/dotnet")]
    [TestCase("/runtime/dotnet.exe", null, "/runtime/dotnet.exe")]
    [TestCase("/plugin/app", "/runtime/dotnet", "/runtime/dotnet")]
    [TestCase(null, null, "dotnet")]
    public void HostSelectionReusesTheCurrentMuxer(string? process, string? configured, string expected)
        => Assert.That(NativeEditorSession.SelectDotnetHost(process, configured), Is.EqualTo(expected));
}

