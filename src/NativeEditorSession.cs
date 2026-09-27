using System.Diagnostics;
using System.Text.Json;

namespace Ziopuzzle.CustomButton;

public sealed class NativeEditorSession : IAsyncDisposable
{
    private readonly object gate = new();
    private Process? process;
    private Task reader = Task.CompletedTask;
    private volatile bool disposed;
    private readonly CancellationTokenSource lifetime = new();
    public bool IsOpen { get { lock (gate) return process is { HasExited: false }; } }
    public void Open(string layout, string values, Func<string> currentLayout, Action<string> apply, Action<string> status)
    {
        lock (gate)
        {
            if (disposed) return;
            if (process is { HasExited: false }) { status("The editor is already open. Switch to it from your taskbar."); return; }
            var path = EditorAssemblyPath(AppContext.BaseDirectory);
            if (!File.Exists(path)) { status("Editor executable not found. Reinstall a package that includes the native editor."); return; }
            process?.Dispose();
            // In a framework-dependent package this is the exact muxer selected by Macro Deck.
            var muxer = SelectDotnetHost(Environment.ProcessPath, Environment.GetEnvironmentVariable("DOTNET_HOST_PATH"));
            var child = Process.Start(EditorStartInfo(AppContext.BaseDirectory, muxer))
                ?? throw new InvalidOperationException("Could not start the editor.");
            process = child;
            child.StandardInput.WriteLine(JsonSerializer.Serialize(new { layout, initialValues = values }));
            child.StandardInput.Flush();
            status("Editor opened. Changes are applied to this draft. Save in Macro Deck to finish.");
            reader = ReadAsync(child, currentLayout, apply, status);
        }
    }
    private async Task ReadAsync(Process child, Func<string> currentLayout, Action<string> apply, Action<string> status)
    {
        try
        {
            while (await child.StandardOutput.ReadLineAsync(lifetime.Token) is { } line)
            {
                if (line.Length > 200000) throw new FormatException("The editor response is too large.");
                using var json = JsonDocument.Parse(line);
                var root = json.RootElement;
                var xml = root.GetProperty("layout").GetString()!;
                var basis = root.GetProperty("basis").GetString()!;
                _ = new LayoutRenderer(xml);
                if (disposed) return;
                if (currentLayout() != basis)
                {
                    status("The XML was changed elsewhere, so editor updates were stopped. Copy your work before reopening the editor.");
                    await child.StandardInput.WriteLineAsync("{\"conflict\":true}");
                    await child.StandardInput.FlushAsync();
                    continue;
                }
                apply(xml);
                status("Editor changes applied to the draft. Save in Macro Deck to finish.");
            }
            await child.WaitForExitAsync(lifetime.Token);
            if (!disposed && child.ExitCode != 0) status($"The editor exited with code {child.ExitCode}. Check OS dependencies and the installed package.");
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception e) when (e is IOException or InvalidOperationException or System.Xml.XmlException or FormatException or JsonException)
        { if (!disposed) status("Editor communication failed: " + e.Message); }
    }
    public static string EditorAssemblyPath(string baseDirectory)
        => Path.Combine(baseDirectory, "Ziopuzzle.CustomButton.Editor.dll");
    public static string SelectDotnetHost(string? processPath, string? configuredHost)
        => Path.GetFileName(processPath) is "dotnet" or "dotnet.exe" ? processPath!
            : !string.IsNullOrWhiteSpace(configuredHost) ? configuredHost : "dotnet";
    public static ProcessStartInfo EditorStartInfo(string baseDirectory, string dotnetHost)
    {
        var start = new ProcessStartInfo(dotnetHost)
        {
            UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true,
            CreateNoWindow = true, WorkingDirectory = baseDirectory
        };
        start.ArgumentList.Add(EditorAssemblyPath(baseDirectory));
        return start;
    }
    public async ValueTask DisposeAsync()
    {
        Task pending;
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            lifetime.Cancel();
            try { if (process is { HasExited: false }) process.StandardInput.Close(); }
            catch (Exception e) when (e is InvalidOperationException or IOException) { /* The editor may already have exited. */ }
            pending = reader;
        }
        await pending;
        process?.Dispose();
        lifetime.Dispose();
    }
}
