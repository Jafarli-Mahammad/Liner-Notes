using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Discovery;
using LinerNotes.Domain.Scoring;

namespace LinerNotes.Application.Features.Discovery;

internal static class SelectDiscoveryResults
{
    internal static (IReadOnlyList<BaselineAPick> Picks, SelectionReceipt Receipt) Apply(
        IReadOnlyList<BaselineAPick> scored, IReadOnlyList<HydratedCandidateTrack> hydrated,
        IReadOnlyList<SelectionInterest> interests, IReadOnlySet<string> recordingSeeds, IReadOnlySet<string> excluded,
        SelectionConfiguration configuration, TasteOnboarding? answers, int count, IReadOnlyDictionary<string, int>? recentArtists)
    {
        var hydratedByKey = hydrated.ToDictionary(c => c.Track.TrackKey, StringComparer.Ordinal);
        var labels = interests.GroupBy(i => i.Identity, StringComparer.Ordinal).ToDictionary(g => g.Key,
            g => g.Select(i => i.Label).Where(label => label is not null).Order(StringComparer.Ordinal).FirstOrDefault(), StringComparer.Ordinal);
        var considered = scored.Select(p =>
        {
            var candidate = hydratedByKey[p.Track.TrackKey];
            var best = ConnectedSelection.BestInterest(candidate.TagVector, interests, configuration.MinimumIndividualSharedTags);
            string? recordingSeed = candidate.RawCandidate.Paths.Where(path => path.SimilarityResponse is not null).Select(path => path.Seed)
                .Where(recordingSeeds.Contains).Order(StringComparer.Ordinal).FirstOrDefault();
            return new SelectionCandidate(p.Track.TrackKey, TrackIdentity.Aliases(p.Track).Where(alias => alias != p.Track.TrackKey).Distinct(StringComparer.Ordinal).ToArray(), ArtistKeys(candidate),
                p.Breakdown.FinalScore, Math.Clamp(p.Breakdown.TagSimilarityRaw * p.Breakdown.Snapshot!.Evidence.SimilarityInfluence, 0, 1),
                best.Affinity, best.Identity, recordingSeed, p.Breakdown.NoveltyRaw,
                DiscoverySelectionPolicy.VocalMatch(p.Breakdown.Snapshot.Evidence, "vocals"), best.Identity is null ? null : labels.GetValueOrDefault(best.Identity));
        }).ToArray();
        var selection = ConnectedSelection.Select(new(configuration, answers, count, considered,
            excluded.Intersect(considered.SelectMany(c => c.Aliases.Append(c.TrackKey)), StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(), recentArtists ?? new Dictionary<string, int>()));
        var decisionsByKey = selection.Decisions.ToDictionary(d => d.TrackKey, StringComparer.Ordinal);
        var consideredByKey = considered.ToDictionary(c => c.TrackKey, StringComparer.Ordinal);
        var byKey = scored.ToDictionary(p => p.Track.TrackKey, StringComparer.Ordinal);
        // Every candidate input is retained; nonselected decisions are reconstructed by replay.
        var storedSelection = selection with { SelectedDecisionsOnly = true, Decisions = selection.Decisions.Where(d => d.Rank.HasValue).ToArray() };
        var picks = selection.SelectedKeys.Select(key =>
        {
            var pick = byKey[key];
            var snapshot = pick.Breakdown.Snapshot!;
            var decision = decisionsByKey[key];
            var candidate = consideredByKey[key];
            return pick with
            {
                Breakdown = pick.Breakdown with
                {
                    Snapshot = snapshot with
                    {
                        Evidence = snapshot.Evidence with
                        {
                            Selection = storedSelection,
                            SelectionReasons = Reasons(candidate, decision, answers)
                        }
                    }
                }
            };
        }).ToArray();
        return (picks, selection);
    }

    private static IReadOnlyList<string> ArtistKeys(HydratedCandidateTrack candidate) => candidate.CreditedArtistIds.Count == 0
        ? ["name:" + candidate.Track.NormalizedArtistName]
        : candidate.CreditedArtistIds.Distinct().Order().Select(id => "artist:" + id.ToString("D")).ToArray();

    private static IReadOnlyList<string> Reasons(SelectionCandidate candidate, SelectionDecision decision, TasteOnboarding? answers)
    {
        var reasons = new List<string>();
        if (candidate.RecordingSeed is not null) reasons.Add("Connected through a selected favorite recording; the provider relationship does not establish acoustic similarity.");
        if (decision.IndividualAdjustment > 0) reasons.Add($"Affinity with {candidate.InterestLabel ?? "one of your saved interests"} contributed {decision.IndividualAdjustment:F3} to selection.");
        if (candidate.AggregateAffinity == 0) reasons.Add("Descriptive tags are missing or have no supported overlap; no aggregate tag affinity was inferred.");
        if (decision.Rank != decision.RankWithoutGoal) reasons.Add("Your discovery goal changed this pick's selected position.");
        if (decision.Rank != decision.RankWithoutExploration) reasons.Add("Your exploration setting changed this pick's selected position.");
        if (decision.Rank != decision.RankWithoutVocals) reasons.Add("Supported recording-level vocal evidence changed this pick's selected position.");
        if (decision.Rank != decision.RankWithoutHistory) reasons.Add("Recent artist concentration changed this pick's selected position.");
        if (answers?.Vocals is "vocals" or "instrumental" && candidate.VocalEvidence == 0)
            reasons.Add("Track-level vocal information is unknown or conflicting; no vocal preference was inferred.");
        reasons.Add($"Selection score: {decision.Utility:F3}. Popularity was not used.");
        return reasons;
    }

}
