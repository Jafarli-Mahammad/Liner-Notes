using LinerNotes.Domain.Scoring;

namespace LinerNotes.Domain.Discovery;

public static class DiscoverySelectionPolicy
{
    public static IReadOnlyList<T> Window<T>(IReadOnlyList<T> values, int count, long round)
    {
        if (count < 1 || round < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (values.Count <= count) return values.ToArray();
        int start = (int)((round % values.Count * count) % values.Count);
        return Enumerable.Range(0, count).Select(i => values[(start + i) % values.Count]).ToArray();
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
