using LinerNotes.Domain.Taste;

namespace LinerNotes.Domain.Scoring;

/// <summary>
/// Pure, deterministic recommendation scoring and ranking engine.
/// Guarantees:
/// 1. Zero I/O, zero hidden state, zero randomness.
/// 2. Deterministic execution: identical inputs produce identical outputs every single run.
/// 3. Fully explainable: every pick generates an inspectable ScoreBreakdown.
/// </summary>
public sealed class RecommendationScorer
{
    /// <summary>
    /// Computes the score breakdown for a single candidate track against a user taste profile.
    /// Pure function with no side effects.
    /// </summary>
    public ScoreBreakdown Score(
        CandidateTrack candidate,
        UserTasteProfile userTaste,
        ScoringParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(userTaste);
        ArgumentNullException.ThrowIfNull(parameters);

        // 1. Tag-overlap similarity via cosine similarity
        var rawSimilarity = candidate.TagVector.CosineSimilarity(userTaste.TagPreferences);
        var similarityScore = parameters.TagSimilarityWeight * rawSimilarity;

        // Retrieve top overlapping tags contributing to similarity
        var matchedTags = candidate.TagVector
            .GetTopOverlappingTags(userTaste.TagPreferences, limit: 5)
            .Select(m => new MatchedTagContribution(m.TagName, m.LeftWeight, m.RightWeight, m.Contribution))
            .ToList();

        // 2. Anti-popularity penalty
        var popularityRaw = candidate.GlobalPopularity;
        var popularityPenalty = parameters.PopularityPenaltyWeight * popularityRaw;

        // 3. Novelty adjustment (prefers unencountered tracks)
        var familiarity = DetermineFamiliarity(candidate, userTaste);
        var noveltyRaw = Math.Clamp(1.0 - familiarity, 0.0, 1.0);
        var noveltyBoost = parameters.NoveltyBoostWeight * noveltyRaw;

        // 4. Feedback penalty (explicit past rejections)
        var feedbackPenaltyRaw = DetermineFeedbackPenalty(candidate, userTaste);
        var feedbackPenalty = parameters.FeedbackPenaltyWeight * feedbackPenaltyRaw;

        // Final aggregate score
        var finalScore = similarityScore - popularityPenalty + noveltyBoost - feedbackPenalty;

        return new ScoreBreakdown(
            finalScore: finalScore,
            tagSimilarityRaw: rawSimilarity,
            tagSimilarityScore: similarityScore,
            popularityRaw: popularityRaw,
            popularityPenalty: popularityPenalty,
            noveltyRaw: noveltyRaw,
            noveltyBoost: noveltyBoost,
            feedbackPenaltyRaw: feedbackPenaltyRaw,
            feedbackPenalty: feedbackPenalty,
            matchedTags: matchedTags);
    }

    /// <summary>
    /// Scores and ranks a collection of candidate tracks.
    /// Ordering is strictly deterministic: sorted descending by FinalScore,
    /// with ties broken deterministically by CandidateKey (ordinal comparison).
    /// </summary>
    public IReadOnlyList<ScoredCandidate> Rank(
        IEnumerable<CandidateTrack> candidates,
        UserTasteProfile userTaste,
        ScoringParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(userTaste);
        ArgumentNullException.ThrowIfNull(parameters);

        var scored = new List<(CandidateTrack Candidate, ScoreBreakdown Breakdown)>();

        foreach (var candidate in candidates)
        {
            var breakdown = Score(candidate, userTaste, parameters);
            scored.Add((candidate, breakdown));
        }

        // Deterministic sort: FinalScore descending, then CandidateKey ordinal ascending
        var ranked = scored
            .OrderByDescending(s => s.Breakdown.FinalScore)
            .ThenBy(s => s.Candidate.CandidateKey, StringComparer.Ordinal)
            .Select((s, index) => new ScoredCandidate(s.Candidate, s.Breakdown, index + 1))
            .ToList();

        return ranked;
    }

    private static double DetermineFamiliarity(CandidateTrack candidate, UserTasteProfile userTaste)
    {
        if (candidate.ExplicitFamiliarity.HasValue)
            return candidate.ExplicitFamiliarity.Value;

        if (userTaste.IsTrackFamiliar(candidate.Track.TrackKey))
            return 1.0;

        if (userTaste.IsArtistFamiliar(candidate.Track.ArtistName))
            return 0.6;

        return 0.0;
    }

    private static double DetermineFeedbackPenalty(CandidateTrack candidate, UserTasteProfile userTaste)
    {
        if (userTaste.IsTrackRejected(candidate.Track.TrackKey))
            return 1.0;

        if (userTaste.IsArtistRejected(candidate.Track.ArtistName))
            return 0.7;

        return 0.0;
    }
}
