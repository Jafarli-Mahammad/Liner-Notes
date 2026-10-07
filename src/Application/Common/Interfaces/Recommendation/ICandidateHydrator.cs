using LinerNotes.Application.Common.Models.Recommendation;

namespace LinerNotes.Application.Common.Interfaces.Recommendation;

/// <summary>
/// Enriches raw discovery metadata in memory, preserving scope, provenance and missing evidence.
/// </summary>
public interface ICandidateHydrator
{
    /// <summary>
    /// Enriches a single raw candidate track with supplied artist tag evidence.
    /// </summary>
    /// <param name="rawCandidate">The raw candidate track.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A transient snapshot; legacy scorer conversion is a later pipeline decision.</returns>
    Task<HydratedCandidateTrack?> HydrateCandidateAsync(
        RawCandidateTrack rawCandidate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch-enriches candidates while retaining explicit failure/coverage gaps.
    /// </summary>
    /// <param name="rawCandidates">Collection of raw candidate tracks.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of fully hydrated domain CandidateTrack instances.</returns>
    Task<IngestionResult<HydratedCandidateTrack>> HydrateCandidatesBatchAsync(
        IReadOnlyList<RawCandidateTrack> rawCandidates,
        CancellationToken cancellationToken = default);
}
