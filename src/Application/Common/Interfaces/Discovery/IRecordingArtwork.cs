namespace LinerNotes.Application.Common.Interfaces.Discovery;

public sealed record RecordingCover(byte[] Bytes, string ContentType);

public interface IRecordingArtwork
{
    RecordingCover? GetCached(Guid recordingId);
    Task WarmAsync(IReadOnlyList<Guid> recordingIds, CancellationToken ct);
}
