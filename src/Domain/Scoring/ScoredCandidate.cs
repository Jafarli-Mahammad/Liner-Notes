namespace LinerNotes.Domain.Scoring;

/// <summary>
/// A candidate track scored and ranked by the pure recommendation scorer.
/// </summary>
public sealed record ScoredCandidate(
    CandidateTrack Candidate,
    ScoreBreakdown Breakdown,
    int Rank)
{
    public override string ToString() =>
        $"#{Rank}: {Candidate.Track} (Score: {Breakdown.FinalScore:F3})";
}
