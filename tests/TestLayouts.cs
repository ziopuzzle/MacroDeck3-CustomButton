using System.Text.Json;
namespace Ziopuzzle.CustomButton.Tests;

// A populated drawing fixture, deliberately independent of new-widget defaults and user-edited examples.
internal static class TestLayouts
{
    internal const string SampleLayout = "<stack id='content'><text id='title'>{{title}}</text><text id='value'>{{value}}%</text><bar id='level' value='{{value}}' min='0' max='100'/></stack>";
    internal static ButtonSettings Sample => new("demo", SampleLayout, "{\"title\":\"CPU\",\"value\":42}");
    internal static string SampleData => JsonSerializer.Serialize(new {channel = Sample.Channel, layout = Sample.Layout, initialValues = Sample.InitialValues, designPreset = "xml", flows = Array.Empty<object>()});
}
