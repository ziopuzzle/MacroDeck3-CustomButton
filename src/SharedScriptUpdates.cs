using MacroDeck.Sdk.Actions;

namespace Ziopuzzle.CustomButton;

// A widget's live views and draft preview share one runner for the same script.
// Different widgets retain their own variable scope, even on the same channel.
public sealed class SharedScriptUpdates
{
    public readonly record struct Key(string? OwnerWidgetId, string ScriptId);
    private readonly object gate = new();
    private readonly Dictionary<Key, Runner> runners = [];

    public Lease Acquire(Key key, Func<CancellationToken, Task<ActionResult>> run)
    {
        lock (gate)
        {
            runners.TryGetValue(key, out var runner);
            if (runner == null || runner.Users == 0) runners[key] = runner = new Runner(run, runner?.Completion);
            runner.Users++;
            return new Lease(this, key, runner);
        }
    }
    private async ValueTask Release(Key key, Runner runner)
    {
        lock (gate)
        {
            if (--runner.Users > 0) return;
        }
        await runner.Stop();
        lock (gate)
            if (runners.TryGetValue(key, out var current) && ReferenceEquals(current, runner)) runners.Remove(key);
    }
    public sealed class Lease : IAsyncDisposable
    {
        private readonly SharedScriptUpdates owner;
        private readonly Key key;
        private Runner? runner;
        internal Lease(SharedScriptUpdates owner, Key key, Runner runner)
        { this.owner = owner; this.key = key; this.runner = runner; }
        public string? Error => Volatile.Read(ref runner)?.Error;
        public ValueTask DisposeAsync()
        {
            var released = Interlocked.Exchange(ref runner, null);
            return released == null ? ValueTask.CompletedTask : owner.Release(key, released);
        }
    }
    internal sealed class Runner
    {
        public int Users;
        private readonly CancellationTokenSource lifetime = new();
        private readonly Task loop;
        public Task Completion => loop;
        private string? error;
        public string? Error => Volatile.Read(ref error);
        public Runner(Func<CancellationToken, Task<ActionResult>> run, Task? predecessor = null) => loop = Task.Run(async () =>
        {
            try
            {
                if (predecessor != null) await predecessor.WaitAsync(lifetime.Token);
                while (!lifetime.IsCancellationRequested)
                {
                    string? nextError = null;
                    try
                    {
                        var result = await run(lifetime.Token);
                        if (result.Status != ActionResultStatus.Succeeded) nextError = "Display update script failed: " + result.ErrorCode;
                    }
                    catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { return; }
                    catch (Exception) { nextError = "Could not run the display update script."; }
                    Volatile.Write(ref error, nextError);
                    await Task.Delay(TimeSpan.FromSeconds(1), lifetime.Token);
                }
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        });
        public async ValueTask Stop()
        {
            await lifetime.CancelAsync();
            await loop;
            lifetime.Dispose();
        }
    }
}
