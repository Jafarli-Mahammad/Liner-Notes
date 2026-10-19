using LinerNotes.Application.Common.Models.Recommendation;

namespace LinerNotes.Application.Common.Interfaces.Recommendation;

public interface IRecordingRecommendationSource
{
    Task<IngestionResult<RawCandidateTrack>> GetCandidatesByRecordingsAsync(IReadOnlyList<Guid> ids, int limit, CancellationToken ct);
}

public interface IRecordingSeedTagSource
{
    Task<SeedTagSnapshot> GetRecordingAsync(Guid id, string name, CancellationToken ct);
}
