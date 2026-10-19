namespace LinerNotes.Application.Common.Interfaces.Discovery;
/// <summary>Explicit operator verification, never part of ordinary page reads or per-job admission.</summary>
public interface IManagedStorageIntegrity { Task VerifyAsync(CancellationToken ct); }
