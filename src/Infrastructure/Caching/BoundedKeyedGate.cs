namespace LinerNotes.Infrastructure.Caching;

// Active owners and waiters retain keys; cancellation and the last release remove them.
internal sealed class BoundedKeyedGate(int maximumKeys) : IDisposable
{
    private readonly object sync = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private bool disposed;

    public async Task<IDisposable> AcquireAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Entry entry;
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!entries.TryGetValue(key, out entry!))
            {
                if (entries.Count >= maximumKeys)
                    throw new LinerNotes.Application.Common.Interfaces.Discovery.PublicPreparationBusyException();
                entries.Add(key, entry = new());
            }
            entry.Users++;
        }
        try { await entry.Gate.WaitAsync(ct); return new Lease(this, key, entry); }
        catch { ReleaseReference(key, entry); throw; }
    }

    private void ReleaseReference(string key, Entry entry)
    {
        lock (sync)
            if (--entry.Users == 0) { entries.Remove(key); entry.Gate.Dispose(); }
    }

    public void Dispose() { lock (sync) disposed = true; }

    private sealed class Entry
    {
        public readonly SemaphoreSlim Gate = new(1, 1);
        public int Users;
    }

    private sealed class Lease(BoundedKeyedGate owner, string key, Entry entry) : IDisposable
    {
        private int released;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref released, 1) != 0) return;
            entry.Gate.Release();
            owner.ReleaseReference(key, entry);
        }
    }
}
