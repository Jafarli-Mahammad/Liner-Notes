namespace LinerNotes.Application.Common.Interfaces.Discovery;

/// <summary>Prevents synchronous framework persistence from recursively waiting on its own admission.</summary>
public static class ManagedWriteBoundary
{
    private static readonly AsyncLocal<int> Depth = new();
    public static bool IsActive => Depth.Value != 0;
    public static IDisposable Enter()
    {
        Depth.Value++;
        return new Scope();
    }
    private sealed class Scope : IDisposable
    {
        private bool disposed;
        public void Dispose() { if (!disposed) { disposed = true; Depth.Value--; } }
    }
}
