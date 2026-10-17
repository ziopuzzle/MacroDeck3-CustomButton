using MacroDeck.Sdk.Ui;
using MacroDeck.Ui.Model.Resources;
using System.Security.Cryptography;
using MacroDeck.Sdk.MusicPlayer;

namespace Ziopuzzle.CustomButton;

/// <summary>One registered slot per image element. Source changes replace, rather than accumulate, resources.</summary>
public sealed class SessionImages : IAsyncDisposable
{
    private sealed class Slot(string name)
    {
        public string Name = name;
        public string Source = "";
        public UiResource? Resource;
        public double AspectRatio = 1;
        public Task Work = Task.CompletedTask;
        public DateTime RetryAfter;
        public DateTime StartAfter;
        public string? Fingerprint;
        public int Failures;
        public bool Active = true;
        public string ColorKey = "", Fallback = "#00000000";
        public string? Color, ColorFingerprint;
        public bool ColorReady;
        public Task ColorWork = Task.CompletedTask;
        public DateTime ColorAfter;
        public long Generation;
    }
    private readonly object gate = new();
    private readonly Dictionary<string, Slot> slots = new();
    private readonly string prefix = "image-" + Guid.NewGuid().ToString("N");
    private readonly IUiResourceRegistry registry;
    private readonly HttpClient http;
    private readonly Uri? hostUrl;
    private readonly Action<string>? diagnostic;
    private readonly TimeProvider clock;
    private readonly IMusicPlayerApi? players;
    private readonly Func<string, CancellationToken, Task<string?>>? readArtworkColor;
    private readonly CancellationTokenSource lifetime = new();
    private long revision;
    private bool disposed;
    public long Revision => Interlocked.Read(ref revision);
    public bool NeedsRefresh
    {
        get { lock (gate) return slots.Values.Any(s => s.Active &&
            (s.Work.IsCompleted && clock.GetUtcNow().UtcDateTime >= s.RetryAfter && clock.GetUtcNow().UtcDateTime >= s.StartAfter
            || s.ColorKey.Length > 0 && s.ColorWork.IsCompleted && clock.GetUtcNow().UtcDateTime >= s.ColorAfter)); }
    }
    public SessionImages(IUiResourceRegistry registry, HttpClient http, Action<string>? diagnostic = null, Uri? hostUrl = null, TimeProvider? clock = null, IMusicPlayerApi? players = null,
        Func<string, CancellationToken, Task<string?>>? readArtworkColor = null)
    { this.registry = registry; this.http = http; this.diagnostic = diagnostic; this.hostUrl = hostUrl; this.clock = clock ?? TimeProvider.System; this.players = players; this.readArtworkColor = readArtworkColor; }

    public void ConfigureColor(string id, string key, string fallback)
    {
        lock (gate)
        {
            if (disposed) return;
            if (!slots.TryGetValue(id, out var slot)) slots[id] = slot = new(prefix + "-" + slots.Count);
            var normalized = key.Length == 0 ? "#00000000" : DisplayColor.Parse(fallback).Hex;
            if (slot.ColorKey == key && slot.Fallback == normalized) return;
            slot.ColorKey = key; slot.Fallback = normalized;
            slot.RetryAfter = default;
        }
    }
    public IReadOnlyDictionary<string, string> Colors()
    {
        lock (gate) return slots.Values.Where(s => s.ColorKey.Length > 0)
            .ToDictionary(s => s.ColorKey, s => s.Color ?? s.Fallback, StringComparer.Ordinal);
    }
    public IReadOnlyDictionary<string, string> CompletedColors()
    {
        lock (gate) return slots.Values.Where(s => s.Active && s.ColorKey.Length > 0 && s.ColorReady)
            .ToDictionary(s => s.ColorKey, s => s.Color ?? s.Fallback, StringComparer.Ordinal);
    }

    // Hidden or removed elements must not keep requesting render passes when their refresh is due.
    public void BeginRender() { lock (gate) foreach (var slot in slots.Values) slot.Active = false; }

    public UiResource? Resolve(string id, string source)
        => ResolveImage(id, source).Resource;
    public (UiResource? Resource, double AspectRatio) ResolveImage(string id, string source)
    {
        lock (gate)
        {
            if (disposed) return (null, 1);
            if (!slots.TryGetValue(id, out var slot)) slots[id] = slot = new(prefix + "-" + slots.Count);
            slot.Active = true;
            if (slot.Source != source) { slot.Source = source; slot.RetryAfter = default; slot.Failures = 0; slot.ColorAfter = default; slot.Generation++; }
            if (source is "" or "—") slot.Resource = null;
            if (slot.Work.IsCompleted && clock.GetUtcNow().UtcDateTime >= slot.RetryAfter && clock.GetUtcNow().UtcDateTime >= slot.StartAfter)
            {
                slot.RetryAfter = DateTime.MaxValue;
                slot.StartAfter = source.StartsWith(SvgTemplate.Prefix, StringComparison.Ordinal) ? clock.GetUtcNow().UtcDateTime.AddMilliseconds(200) : default;
                slot.Work = Task.Run(() => LoadAsync(slot, source));
            }
            if (slot.ColorKey.Length > 0 && slot.ColorWork.IsCompleted && clock.GetUtcNow().UtcDateTime >= slot.ColorAfter)
            {
                slot.ColorAfter = DateTime.MaxValue;
                if (HostImageSource.Parse(source)?.InstanceId is { } player)
                {
                    var generation = slot.Generation;
                    slot.ColorWork = Task.Run(() => RefreshArtworkColorAsync(slot, player, generation));
                }
            }
            return (slot.Resource, slot.AspectRatio);
        }
    }
    private async Task LoadAsync(Slot slot, string source)
    {
        var stage = "download";
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            UiResource? resource = null;
            var aspectRatio = 1d;
            string? fingerprint = null;
            string? average = null;
            string colorKey;
            lock (gate) colorKey = slot.ColorKey;
            if (source is "" or "—") { stage = "remove"; await registry.RemoveAsync(slot.Name, timeout.Token); }
            else if (HostImageSource.Parse(source) is { } hostImage)
            {
                stage = "host lookup";
                if (hostImage.IconId is { } iconId)
                {
                    resource = await registry.GetIconAsync(iconId, timeout.Token);
                    // Installed icons are host-owned handles, not our named resource slots.
                    await registry.RemoveAsync(slot.Name, timeout.Token);
                }
                else
                    resource = await registry.RegisterMusicPlayerArtworkAsync(slot.Name, hostImage.InstanceId!, hostImage.ArtworkId!, timeout.Token);
                if (resource is null) throw new FormatException("The requested host image is unavailable.");
                // The handle carries no dimensions. Let the client frame the decoded image.
                aspectRatio = double.NaN;
            }
            else
            {
                // Conservative local cap. The host also enforces its negotiated resource quotas.
                var image = await ImageSourceLoader.LoadAsync(source, http, 2 * 1024 * 1024, timeout.Token, hostUrl);
                aspectRatio = image.AspectRatio;
                fingerprint = Convert.ToHexString(SHA256.HashData(image.Bytes));
                // Only content changes require an upload; periodic checks must not restart GIFs/crossfades.
                lock (gate) { if (slot.Fingerprint == fingerprint) resource = slot.Resource; }
                stage = "register";
                resource ??= await registry.RegisterAsync(slot.Name, image.Bytes, image.MediaType, timeout.Token);
                if (colorKey.Length > 0)
                {
                    lock (gate) if (slot.ColorFingerprint == fingerprint) average = slot.Color;
                    if (average == null)
                    {
                        try { average = ImageAverageColor.Calculate(image.Bytes); }
                        catch (FormatException e) { diagnostic?.Invoke(e.Message); }
                    }
                }
            }
            lock (gate)
            {
                // An obsolete upload may have changed the host slot even though the current source differs.
                slot.Fingerprint = fingerprint;
                if (slot.Source == source)
                {
                    slot.Resource = resource; slot.AspectRatio = aspectRatio;
                    if (HostImageSource.Parse(source)?.InstanceId == null)
                    { slot.Color = average; slot.ColorFingerprint = fingerprint; slot.ColorReady = true; }
                    slot.Failures = 0;
                    slot.RetryAfter = source is "" or "—" || source.StartsWith(SvgTemplate.Prefix, StringComparison.Ordinal)
                        ? DateTime.MaxValue : clock.GetUtcNow().UtcDateTime.AddSeconds(30);
                }
            }
        }
        catch (Exception e)
        {
            if (!lifetime.IsCancellationRequested)
            {
                // Do not log URLs: cover-art URLs can contain access tokens.
                var reason = e switch
                {
                    HttpRequestException error => "HTTP " + (error.StatusCode?.ToString() ?? "transport error"),
                    UiResourceException error => "host resource " + error.ErrorCode,
                    FormatException => e.Message,
                    OperationCanceledException => "timeout",
                    _ => e.GetType().Name
                };
                lock (gate)
                {
                    if (slot.Source == source)
                    {
                        slot.Failures = Math.Min(slot.Failures + 1, 6);
                        var seconds = Math.Min(30, 1 << (slot.Failures - 1));
                        slot.RetryAfter = clock.GetUtcNow().UtcDateTime.AddSeconds(seconds);
                        diagnostic?.Invoke($"Image {stage} failed ({reason}); retry in {seconds}s. Keeping the last available image.");
                    }
                }
            }
        }
        finally
        {
            lock (gate)
            {
                // A source may change back while an older upload is in flight.
                // Always reconcile the host slot with the latest requested source.
                if (slot.Source != source) { slot.Fingerprint = null; slot.RetryAfter = default; }
            }
            Interlocked.Increment(ref revision);
        }
    }
    private async Task RefreshArtworkColorAsync(Slot slot, string player, long generation)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            var color = readArtworkColor != null ? await readArtworkColor(player, timeout.Token)
                : players == null ? null : await players.GetAlbumColorAsync(player, timeout.Token).WaitAsync(timeout.Token);
            if (color != null) color = DisplayColor.Parse(color).Hex;
            lock (gate)
            {
                if (disposed || slot.Generation != generation) return;
                if (!slot.ColorReady || slot.Color != color)
                {
                    slot.Color = color; slot.ColorReady = true;
                    Interlocked.Increment(ref revision);
                }
            }
        }
        catch (Exception) when (!lifetime.IsCancellationRequested)
        {
            // Keep the previous colour on transport errors; retry without restarting image loading.
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        finally
        {
            lock (gate) slot.ColorAfter = slot.Generation == generation ? clock.GetUtcNow().UtcDateTime.AddSeconds(1) : default;
        }
    }
    public async ValueTask DisposeAsync()
    {
        Slot[] entries;
        lock (gate) { if (disposed) return; disposed = true; entries = slots.Values.ToArray(); }
        await lifetime.CancelAsync();
        await Task.WhenAll(entries.Select(s => s.Work));
        await Task.WhenAll(entries.Select(s => s.ColorWork));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        foreach (var slot in entries)
        {
            try { await registry.RemoveAsync(slot.Name, timeout.Token); }
            catch (Exception) { /* Session teardown also releases resources at the host. */ }
        }
        lifetime.Dispose();
    }
}
