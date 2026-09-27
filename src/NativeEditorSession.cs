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
            var path = EditorExecutablePath(AppContext.BaseDirectory, OperatingSystem.IsWindows());
            if (!File.Exists(path)) { status("Editor executable not found. Reinstall a package that includes the native editor."); return; }
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(path, File.GetUnixFileMode(path) | UnixFileMode.UserExecute);
            process?.Dispose();
            var child = Process.Start(new ProcessStartInfo(path)
            {
                UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true,
                CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(path)!
            }) ?? throw new InvalidOperationException("Could not start the editor.");
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
    public static string EditorExecutablePath(string baseDirectory, bool windows)
        => Path.Combine(baseDirectory, "Ziopuzzle.CustomButton.Editor" + (windows ? ".exe" : ""));
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
