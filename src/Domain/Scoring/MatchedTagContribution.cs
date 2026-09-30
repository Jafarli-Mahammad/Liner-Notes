namespace LinerNotes.Domain.Scoring;

/// <summary>
/// Detailed breakdown of an individual tag's contribution to candidate similarity.
/// Persisted as part of the score breakdown to explain why a recommendation was picked.
/// </summary>
public sealed record MatchedTagContribution(
    string TagName,
    double CandidateTagWeight,
    double UserTasteWeight,
    double ContributionProduct)
{
    public override string ToString() =>
        $"{TagName} (cand: {CandidateTagWeight:F2}, user: {UserTasteWeight:F2})";
}
