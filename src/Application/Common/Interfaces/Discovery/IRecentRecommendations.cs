namespace LinerNotes.Application.Common.Interfaces.Discovery;

public sealed record RecentMusic(long Sequence, Guid RecordingId, string Title, string ArtistName, string Origin, string Attribution);
public sealed record RecentMusicPage(IReadOnlyList<RecentMusic> Items, long? NextCursor);

public interface IRecentRecommendations
{
    Task<RecentMusicPage> ReadAsync(long? before, int limit, DateTimeOffset now, CancellationToken ct);
}
