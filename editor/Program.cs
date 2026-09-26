using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace Ziopuzzle.CustomButton;

public sealed class EditorApp : Application
{
    internal static string? Input;
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(new Uri("avares://Ziopuzzle.CustomButton.Editor/")) {
            Source = new Uri("avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml") });
    }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && Input != null)
        {
            using var json = JsonDocument.Parse(Input);
            var window = new EditorWindow(json.RootElement.GetProperty("layout").GetString()!, json.RootElement.GetProperty("initialValues").GetString()!);
            desktop.MainWindow = window;
            _ = Task.Run(async () =>
            {
                try
                {
                    while (await Console.In.ReadLineAsync() is { } line)
                    {
                        using var response = JsonDocument.Parse(line);
                        if (response.RootElement.TryGetProperty("conflict", out _)) await Dispatcher.UIThread.InvokeAsync(window.Conflict);
                    }
                }
                catch (Exception e) { Console.Error.WriteLine(e.Message); }
                finally { await Dispatcher.UIThread.InvokeAsync(window.Disconnect); }
            });
        }
        base.OnFrameworkInitializationCompleted();
    }
}

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            EditorApp.Input = Console.ReadLine();
            if (EditorApp.Input == null) return 0;
            return AppBuilder.Configure<EditorApp>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
