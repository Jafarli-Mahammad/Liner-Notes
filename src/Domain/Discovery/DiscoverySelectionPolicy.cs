using LinerNotes.Domain.Scoring;

namespace LinerNotes.Domain.Discovery;

public static class DiscoverySelectionPolicy
{
    public const string Version = "optional-taste-selection-v1";

    public static IReadOnlyList<T> Window<T>(IReadOnlyList<T> values, int count, long round)
    {
        if (count < 1 || round < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (values.Count <= count) return values.ToArray();
        int start = (int)((round % values.Count * count) % values.Count);
        return Enumerable.Range(0, count).Select(i => values[(start + i) % values.Count]).ToArray();
    }

    public static IReadOnlyList<BaselineAPick> Rank(IEnumerable<BaselineAPick> candidates, TasteOnboarding? answers, int count)
    {
        answers?.Validate();
        return candidates.OrderByDescending(p => answers?.Goal == "new-artists" ? p.Breakdown.NoveltyRaw : 0)
            .ThenByDescending(p => answers?.Goal switch
            {
                "favorites" => p.Breakdown.TagSimilarityRaw,
                "different-styles" => -p.Breakdown.TagSimilarityRaw,
                _ => 0
            })
            .ThenByDescending(p => answers?.Exploration switch
            {
                "close" => p.Breakdown.TagSimilarityRaw,
                "surprise" => -p.Breakdown.TagSimilarityRaw,
                _ => 0
            })
            .ThenByDescending(p => VocalMatch(p.Breakdown.Snapshot?.Evidence, answers?.Vocals))
            .ThenByDescending(p => p.Breakdown.FinalScore).ThenBy(p => p.Track.TrackKey, StringComparer.Ordinal)
            .DistinctBy(p => p.Track.NormalizedArtistName, StringComparer.Ordinal).Take(count).ToArray();
    }

    public static int VocalMatch(ScoreEvidence? evidence, string? choice)
    {
        if (choice is not ("vocals" or "instrumental") || evidence?.TagScope != "recording") return 0;
        bool vocals = evidence.CandidateTags.Any(t => t.ExclusionReason is null && t.Name is "vocals" or "vocal" or "vocal jazz" or "singing");
        bool instrumental = evidence.CandidateTags.Any(t => t.ExclusionReason is null && t.Name is "instrumental" or "instrumentals");
        if (vocals == instrumental) return 0;
        return (choice == "vocals" ? vocals : instrumental) ? 1 : -1;
    }
}
