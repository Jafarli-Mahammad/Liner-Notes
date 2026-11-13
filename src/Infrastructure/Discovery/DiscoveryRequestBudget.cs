using LinerNotes.Application.Common.Interfaces.Recommendation;

namespace LinerNotes.Infrastructure.Discovery;

public sealed class DiscoveryRequestBudget : IDiscoveryRequestBudget
{
    private sealed class Counter(int limit) { public readonly int Limit = limit; public int Sent; }
    private readonly AsyncLocal<Counter?> current = new();

    public IDisposable Begin(int maximumRequests)
    {
        if (maximumRequests is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(maximumRequests));
        var previous = current.Value;
        current.Value = previous ?? new Counter(maximumRequests);
        return new Scope(() => current.Value = previous);
    }

    public void Spend()
    {
        if (current.Value is { } counter && Interlocked.Increment(ref counter.Sent) > counter.Limit)
            throw new GenerationStoppedException("upstream_request_limit");
    }

    private sealed class Scope(Action restore) : IDisposable
    {
        private bool disposed;
        public void Dispose() { if (!disposed) { disposed = true; restore(); } }
    }
}
