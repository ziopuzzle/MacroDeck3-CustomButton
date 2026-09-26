using MacroDeck.Sdk.Ui;
using MacroDeck.Ui.Model.Resources;
using System.Security.Cryptography;

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
        public string? Fingerprint;
        public int Failures;
        public bool Active = true;
    }
    private readonly object gate = new();
    private readonly Dictionary<string, Slot> slots = new();
    private readonly string prefix = "image-" + Guid.NewGuid().ToString("N");
    private readonly IUiResourceRegistry registry;
    private readonly HttpClient http;
    private readonly Uri? hostUrl;
    private readonly Action<string>? diagnostic;
    private readonly TimeProvider clock;
    private readonly CancellationTokenSource lifetime = new();
    private long revision;
    private bool disposed;
    public long Revision => Interlocked.Read(ref revision);
    public bool NeedsRefresh
    {
        get { lock (gate) return slots.Values.Any(s => s.Active && s.Work.IsCompleted && clock.GetUtcNow().UtcDateTime >= s.RetryAfter); }
    }
    public SessionImages(IUiResourceRegistry registry, HttpClient http, Action<string>? diagnostic = null, Uri? hostUrl = null, TimeProvider? clock = null)
    { this.registry = registry; this.http = http; this.diagnostic = diagnostic; this.hostUrl = hostUrl; this.clock = clock ?? TimeProvider.System; }

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
            if (slot.Source != source) { slot.Source = source; slot.RetryAfter = default; slot.Failures = 0; }
            if (source is "" or "—") slot.Resource = null;
            if (slot.Work.IsCompleted && clock.GetUtcNow().UtcDateTime >= slot.RetryAfter)
            {
                slot.RetryAfter = DateTime.MaxValue;
                slot.Work = Task.Run(() => LoadAsync(slot, source));
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
            if (source is "" or "—") { stage = "remove"; await registry.RemoveAsync(slot.Name, timeout.Token); }
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
            }
            lock (gate)
            {
                // An obsolete upload may have changed the host slot even though the current source differs.
                slot.Fingerprint = fingerprint;
                if (slot.Source == source)
                {
                    slot.Resource = resource; slot.AspectRatio = aspectRatio;
                    slot.Failures = 0;
                    slot.RetryAfter = source is "" or "—" ? DateTime.MaxValue : clock.GetUtcNow().UtcDateTime.AddSeconds(30);
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
    public async ValueTask DisposeAsync()
    {
        Slot[] entries;
        lock (gate) { if (disposed) return; disposed = true; entries = slots.Values.ToArray(); }
        await lifetime.CancelAsync();
        await Task.WhenAll(entries.Select(s => s.Work));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        foreach (var slot in entries)
        {
            try { await registry.RemoveAsync(slot.Name, timeout.Token); }
            catch (Exception) { /* Session teardown also releases resources at the host. */ }
        }
        lifetime.Dispose();
    }
}
