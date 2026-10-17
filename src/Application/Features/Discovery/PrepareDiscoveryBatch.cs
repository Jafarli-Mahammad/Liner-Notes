using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Mappings;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Taste;

namespace LinerNotes.Application.Features.Discovery;

public sealed record PreparedDiscoveryBatch(Guid Id, IReadOnlyList<BaselineAPick> Picks, string CoverageJson,
    DateTimeOffset? EvidenceExpiry, long ReservationBytes);

public sealed class PrepareDiscoveryBatch(MaterializeDiscoveryTaste materializer, IDiscoveryPreparation preparation,
    IDiscoveryBatchStore store, GenerationConfiguration configuration, BaselineAScorer scorer)
{
    public async Task<PreparedDiscoveryBatch> PrepareAsync(DiscoveryInputs inputs, Guid batchId, bool allowAcquisition, CancellationToken ct)
    {
        var scoring = configuration.Scoring();
        if (configuration.Origin == EvidenceOrigin.Recorded) throw new GenerationStoppedException("evaluation_evidence_ineligible");
        var effective = EffectiveTasteInputs.FromProfile(inputs.Profile.Id, inputs.Signals, inputs.Feedback,
            new(configuration.FeedbackTagWeight, configuration.MinimumPositiveFeedbackWeight));
        var taste = await materializer.BuildAsync(effective.PositiveSignals, allowAcquisition, ct);
        var requests = taste.Artists.Select(a => new DiscoveryPoolRequest(a.Id, a.Name, false))
            .Concat(taste.Tags.Select(t => new DiscoveryPoolRequest(null, t, true))).ToArray();
        // Legacy imported artist seeds have no canonical catalog identity.
        var legacy = taste.Seeds.Where(s => s.EntityIdentity?.StartsWith("legacy-artist:", StringComparison.Ordinal) == true)
            .Select(s => new DiscoveryPoolRequest(null, s.ArtistName, false)).Distinct().ToArray();
        int artistSlots = Math.Max(0, configuration.MaximumArtistSeeds - taste.Artists.Count);
        requests = requests.Concat(legacy.Take(artistSlots)).ToArray();
        if (requests.Length > configuration.MaximumArtistSeeds + configuration.MaximumTagSeeds)
            throw new GenerationStoppedException("seed_limit");
        var pools = new List<PreparedDiscoveryPool>();
        foreach (var request in requests) pools.Add(await preparation.GetAsync(request, allowAcquisition, ct));
        if (pools.Any(p => p.Origin != configuration.Origin)) throw new GenerationStoppedException("origin_mismatch");
        var gaps = taste.Gaps.Concat(pools.SelectMany(p => p.Gaps))
            .Concat(legacy.Length > artistSlots ? new[] { new CoverageGap("profile", "discovery", "artist_seed_window_applied") } : [])
            .Distinct().ToArray();
        if (pools.Sum(p => p.Candidates.Count) > configuration.MaximumCandidates) throw new GenerationStoppedException("candidate_limit");
        var hydrated = Merge(pools.SelectMany(p => p.Candidates));
        if (hydrated.Count > configuration.MaximumCandidates) throw new GenerationStoppedException("candidate_limit");
        var aliases = hydrated.SelectMany(c => TrackIdentity.Aliases(c.Track)).Distinct(StringComparer.Ordinal).ToArray();
        var excluded = (await store.FindExcludedAsync(inputs.Profile.Id, aliases, ct)).Concat(effective.ExcludedAliases).ToHashSet(StringComparer.Ordinal);
        var candidates = hydrated.Select(c => new BaselineACandidate(c.Track, c.TagVector,
            new ScoreEvidence(effective.PositiveSignals.Select(s => s.Store()).ToArray(), taste.Seeds,
                c.Tags.Select(t => t.Store()).ToArray(), c.TagResponse.Store(),
                c.RawCandidate.Paths.Select(p => p.Store()).ToArray(), gaps.Concat(c.Gaps).Distinct().Select(g => g.Store()).ToArray(),
                c.TagScope, c.PopularityMissingReason) {
                MaterializationVersion = DiscoveryTasteMaterializer.Version,
                TagNormalizationVersion = c.TagNormalizationVersion,
                SupportingResponses = c.SupportingResponses.Select(r => r.Store()).ToArray()
            })).ToArray();
        var familiar = effective.FamiliarArtists.Concat(taste.FamiliarArtists).ToHashSet(StringComparer.Ordinal);
        var picks = scorer.RankForBatch(candidates, taste.Vector, familiar, excluded, batchId, scoring, configuration.MaximumPicks);
        long bytes = 65_536;
        foreach (var pick in picks)
        {
            int length = JsonSerializer.SerializeToUtf8Bytes(pick.Breakdown).Length;
            if (length > configuration.MaximumSnapshotBytes) throw new GenerationStoppedException("snapshot_limit");
            bytes = checked(bytes + length * 4L + 32_768);
        }
        string coverage = JsonSerializer.Serialize(new {
            Version = "discovery-coverage-v1", SupportedPicks = picks.Count, RequestedPicks = configuration.MaximumPicks,
            CandidateCount = hydrated.Count, SuppressedCandidates = hydrated.Count(c => TrackIdentity.Aliases(c.Track).Any(excluded.Contains)),
            FeedbackConsidered = inputs.Feedback.Count,
            Gaps = gaps.Select(g => new { g.Method, g.Reason }).Distinct().Take(40).ToArray(),
            Origin = configuration.Origin.ToString()
        });
        DateTimeOffset? expiry = pools.Where(p => p.ExpiresAtUtc > p.PreparedAtUtc).Select(p => p.ExpiresAtUtc)
            .Append(taste.EvidenceExpiresAtUtc).Min();
        return new(batchId, picks, coverage, expiry, bytes);
    }

    private static IReadOnlyList<HydratedCandidateTrack> Merge(IEnumerable<HydratedCandidateTrack> candidates)
    {
        var groups = new List<List<HydratedCandidateTrack>>();
        foreach (var candidate in candidates.OrderBy(c => c.Track.TrackKey, StringComparer.Ordinal))
        {
            var aliases = TrackIdentity.Aliases(candidate.Track);
            var matches = groups.Where(g => g.Any(c => TrackIdentity.Aliases(c.Track).Intersect(aliases, StringComparer.Ordinal).Any())).ToArray();
            var group = new List<HydratedCandidateTrack> { candidate };
            foreach (var match in matches) { group.AddRange(match); groups.Remove(match); }
            groups.Add(group);
        }
        return groups.Select(g => {
            var first = g.OrderByDescending(c => c.TagVector.Weights.Count).ThenBy(c => c.Track.TrackKey, StringComparer.Ordinal).First();
            return new HydratedCandidateTrack(first.Track, first.TagVector,
                new(first.Track.Title, first.Track.ArtistName, first.Track.Mbid, g.SelectMany(c => c.RawCandidate.Paths).Distinct().ToArray()),
                first.Tags, first.TagResponse, first.TagMissingReason, g.SelectMany(c => c.Gaps).Distinct()) {
                TagNormalizationVersion = first.TagNormalizationVersion, SupportingResponses = first.SupportingResponses
            };
        }).OrderBy(c => c.Track.TrackKey, StringComparer.Ordinal).ToArray();
    }
}
