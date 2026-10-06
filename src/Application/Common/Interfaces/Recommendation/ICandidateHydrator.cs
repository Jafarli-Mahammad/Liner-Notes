using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Scoring;

namespace LinerNotes.Application.Common.Interfaces.Recommendation;

/// <summary>
/// Abstraction for enriching raw candidate tracks with musical tag vectors and popularity metrics
/// so they can be deterministically scored by the Residue recommendation engine.
/// </summary>
public interface ICandidateHydrator
{
    /// <summary>
    /// Enriches a single raw candidate track with its weighted tag vector and global popularity.
    /// </summary>
    /// <param name="rawCandidate">The raw candidate track.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A fully hydrated domain CandidateTrack ready for Residue scoring, or null if metadata was insufficient.</returns>
    Task<CandidateTrack?> HydrateCandidateAsync(
        RawCandidateTrack rawCandidate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch-enriches multiple raw candidate tracks with their weighted tag vectors and global popularity.
    /// </summary>
    /// <param name="rawCandidates">Collection of raw candidate tracks.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of fully hydrated domain CandidateTrack instances.</returns>
    Task<IReadOnlyList<CandidateTrack>> HydrateCandidatesBatchAsync(
        IReadOnlyList<RawCandidateTrack> rawCandidates,
        CancellationToken cancellationToken = default);
}
