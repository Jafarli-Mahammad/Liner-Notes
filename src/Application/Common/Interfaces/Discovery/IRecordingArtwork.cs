namespace LinerNotes.Application.Common.Interfaces.Discovery;

public sealed record RecordingCover(byte[] Bytes, string ContentType, DateTimeOffset? ExpiresAtUtc = null, bool CanPersist = true);
public enum RecordingArtworkState { Pending, Unavailable, Ready }

public interface IRecordingArtwork
{
    RecordingCover? GetCached(Guid recordingId);
    RecordingArtworkState Request(Guid recordingId);
    Task WarmAsync(IReadOnlyList<Guid> recordingIds, CancellationToken ct, bool allowAcquisition = true);
}

public interface IArtworkQueue
{
    void Enqueue(IReadOnlyList<Guid> recordingIds, bool priority = false);
    Task<IReadOnlyList<Guid>> TakeAsync(CancellationToken ct);
    void Complete(IReadOnlyList<Guid> recordingIds, bool retryable = false);
}

public interface IArtworkCatalog
{
    Task<IReadOnlyList<Guid>> EligibleAsync(IReadOnlyList<Guid> recordingIds, DateTimeOffset now, CancellationToken ct);
}

public interface IStoredArtwork
{
    Task<RecordingCover?> FindAsync(Guid recordingId, CancellationToken ct);
    Task SaveAsync(Guid recordingId, RecordingCover cover, CancellationToken ct);
}
