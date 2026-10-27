using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Mappings;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Taste;

namespace LinerNotes.Application.Features.Discovery;

public sealed record PreparedDiscoveryBatch(Guid Id, IReadOnlyList<BaselineAPick> Picks, string CoverageJson,
    DateTimeOffset? EvidenceExpiry, long ReservationBytes, IReadOnlyList<CoverageGap>? Gaps = null);

public sealed class PrepareDiscoveryBatch(MaterializeDiscoveryTaste materializer, IDiscoveryPreparation preparation,
    IDiscoveryBatchStore store, GenerationConfiguration configuration, BaselineAScorer scorer)
{
    public async Task<PreparedDiscoveryBatch> PrepareAsync(DiscoveryInputs inputs, Guid batchId, bool allowAcquisition, CancellationToken ct, LinerNotes.Domain.Digest.IsoWeek? week = null)
    {
        var scoring = configuration.Scoring();
        if (configuration.Origin == EvidenceOrigin.Recorded) throw new GenerationStoppedException("evaluation_evidence_ineligible");
        var effective = EffectiveTasteInputs.FromProfile(inputs.Profile.Id, inputs.Signals, inputs.Feedback,
            new(configuration.FeedbackTagWeight, configuration.MinimumPositiveFeedbackWeight));
        var taste = await materializer.BuildAsync(effective.PositiveSignals, allowAcquisition, ct, inputs.GenerationOrdinal);
        var answers = OnboardingState.Read(inputs.Profile.OnboardingJson);
        var artistRequests = taste.Artists.Select(a => new DiscoveryPoolRequest(a.Id, a.Name, false))
            .Concat(taste.Recordings.Select(r => new DiscoveryPoolRequest(r.Id, r.Name, false, true)));
        var requests = LinerNotes.Domain.Discovery.DiscoverySelectionPolicy.Window(artistRequests.ToArray(), configuration.MaximumArtistSeeds, inputs.GenerationOrdinal)
            .Concat(taste.Tags.Select(t => new DiscoveryPoolRequest(null, t, true))).ToArray();
        // Legacy imported artist seeds have no canonical catalog identity.
        if (answers?.AllStyles == true)
        {
            var broad = LinerNotes.Domain.Discovery.DiscoverySelectionPolicy.Window(TasteVocabulary.Entries.Where(t => t.Category == "genre").Select(t => t.Id).ToArray(), configuration.MaximumTagSeeds, inputs.GenerationOrdinal);
            requests = requests.Where(r => !r.IsTag).Concat(broad.Select(t => new DiscoveryPoolRequest(null, t, true))).ToArray();
        }
        var legacy = taste.Seeds.Where(s => s.EntityIdentity?.StartsWith("legacy-artist:", StringComparison.Ordinal) == true)
            .Select(s => new DiscoveryPoolRequest(null, s.ArtistName, false)).Distinct().ToArray();
        int artistSlots = Math.Max(0, configuration.MaximumArtistSeeds - requests.Count(r => !r.IsTag));
        requests = requests.Concat(legacy.Take(artistSlots)).ToArray();
        if (requests.Length > configuration.MaximumArtistSeeds + configuration.MaximumTagSeeds)
            throw new GenerationStoppedException("seed_limit");
        var pools = new List<PreparedDiscoveryPool>();
        // Independent public pools have their own scopes/leases. Keep input order for evidence and tie-breaking.
        if (allowAcquisition)
        {
            using var parallelism = new SemaphoreSlim(2);
            var pending = requests.Select(async request =>
            {
                await parallelism.WaitAsync(ct);
                try { return await preparation.GetAsync(request, true, ct); }
                finally { parallelism.Release(); }
            });
            pools.AddRange(await Task.WhenAll(pending));
        }
        else foreach (var request in requests) pools.Add(await preparation.GetAsync(request, false, ct));
        if (pools.Any(p => p.Origin != configuration.Origin)) throw new GenerationStoppedException("origin_mismatch");
        var gaps = taste.Gaps.Concat(pools.SelectMany(p => p.Gaps))
            .Concat(legacy.Length > artistSlots ? new[] { new CoverageGap("profile", "discovery", "artist_seed_window_applied") } : [])
            .Distinct().ToArray();
        if (pools.Sum(p => p.Candidates.Count) > configuration.MaximumCandidates) throw new GenerationStoppedException("candidate_limit");
        var hydrated = Merge(pools.SelectMany(p => p.Candidates));
        if (gaps.Any(g => g.Reason is "permission_denied" or "http_error" or "invalid_json" or "invalid_discovery_shape_or_identifier"))
            throw new DiscoverySourceException("source_configuration_or_response_invalid", false);
        if (hydrated.Count == 0 && gaps.Any(g => g.Reason is "rate_limit" or "transport_error" or "request_timeout" or "upstream_unavailable"))
            throw new DiscoverySourceException("source_temporarily_unavailable", true);
        if (hydrated.Count > configuration.MaximumCandidates) throw new GenerationStoppedException("candidate_limit");
        var aliases = hydrated.SelectMany(c => TrackIdentity.Aliases(c.Track)).Distinct(StringComparer.Ordinal).ToArray();
        var excluded = (await store.FindExcludedAsync(inputs.Profile.Id, aliases, ct)).Concat(effective.ExcludedAliases).ToHashSet(StringComparer.Ordinal);
        string inputHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(effective.PositiveSignals.OrderBy(s => s.NormalizedTargetValue, StringComparer.Ordinal).Select(s => new { s.TargetType, s.NormalizedTargetValue, s.Weight, s.CanonicalEntityId }))));
        var candidates = hydrated.Select(c => new BaselineACandidate(c.Track, c.TagVector,
            new ScoreEvidence(effective.PositiveSignals.Where(s => s.CanonicalEntityId != null ? taste.Seeds.Any(seed => seed.SignalId == s.Id) : c.TagVector.Weights.ContainsKey(s.NormalizedTargetValue)).Take(80).Select(s => s.Store()).ToArray(), taste.Seeds,
                c.Tags.Select(t => t.Store()).ToArray(), c.TagResponse.Store(),
                c.RawCandidate.Paths.Select(p => p.Store()).ToArray(), gaps.Concat(c.Gaps).Distinct().Select(g => g.Store()).ToArray(),
                c.TagScope, c.PopularityMissingReason)
            {
                MaterializationVersion = DiscoveryTasteMaterializer.Version,
                TagNormalizationVersion = c.TagNormalizationVersion,
                SupportingResponses = c.SupportingResponses.Select(r => r.Store()).ToArray(),
                FullInputSha256 = inputHash,
                FullInputCount = effective.PositiveSignals.Count,
                SelectionPolicy = LinerNotes.Domain.Discovery.DiscoverySelectionPolicy.Version,
                SelectionAnswers = answers is null ? null : JsonSerializer.Serialize(answers),
                SelectionReasons = PreferenceReasons(c),
                AcquisitionWindow = requests.Select(r => (r.IsRecording ? "recording:" : r.IsTag ? "tag:" : "artist:") + (r.ArtistId?.ToString("D") ?? r.Seed)).ToArray(),
                AlbumFallbacks = Fallbacks(c),
                SimilarityInfluence = Influence(c)
            })).ToArray();
        var familiar = effective.FamiliarArtists.Concat(taste.FamiliarArtists).ToHashSet(StringComparer.Ordinal);
        var eligible = candidates.Where(c => !TrackIdentity.Aliases(c.Track).Any(excluded.Contains));
        var scored = week is null
            ? scorer.ScoreForBatch(eligible, taste.Vector, familiar, batchId, scoring)
            : scorer.Score(eligible, taste.Vector, familiar, week, scoring);
        var picks = LinerNotes.Domain.Discovery.DiscoverySelectionPolicy.Rank(scored, answers, configuration.MaximumPicks);
        long bytes = 65_536;
        foreach (var pick in picks)
        {
            int length = JsonSerializer.SerializeToUtf8Bytes(pick.Breakdown).Length;
            if (length > configuration.MaximumSnapshotBytes) throw new GenerationStoppedException("snapshot_limit");
            bytes = checked(bytes + length * 4L + 32_768);
        }
        string coverage = JsonSerializer.Serialize(new
        {
            Version = "discovery-coverage-v1",
            SupportedPicks = picks.Count,
            RequestedPicks = configuration.MaximumPicks,
            CandidateCount = hydrated.Count,
            SuppressedCandidates = hydrated.Count(c => TrackIdentity.Aliases(c.Track).Any(excluded.Contains)),
            FeedbackConsidered = inputs.Feedback.Count,
            Gaps = gaps.Select(g => new { g.Method, g.Reason }).Distinct().Take(40).ToArray(),
            Origin = configuration.Origin.ToString()
        });
        DateTimeOffset? expiry = pools.Where(p => p.ExpiresAtUtc > p.PreparedAtUtc).Select(p => p.ExpiresAtUtc)
            .Append(taste.EvidenceExpiresAtUtc).Min();
        return new(batchId, picks, coverage, expiry, bytes, gaps);

        IReadOnlyList<string> PreferenceReasons(HydratedCandidateTrack c)
        {
            var reasons = new List<string>();
            if (answers?.Goal is "new-artists") reasons.Add("Your discovery goal gives unfamiliar artists priority.");
            if (answers?.Goal is "favorites") reasons.Add("Your discovery goal gives stronger supported taste overlap priority.");
            if (answers?.Goal is "different-styles") reasons.Add("Your discovery goal gives less overlapping styles priority among supported candidates.");
            if (answers?.Exploration is "close" or "surprise") reasons.Add(answers.Exploration == "close" ? "You asked to stay close to your taste." : "You asked to explore beyond your strongest taste overlap.");
            if (answers?.AllStyles == true) reasons.Add("All musical styles are welcome; selected favorites remain positive examples.");
            if (answers?.Vocals is "vocals" or "instrumental") reasons.Add(c.TagScope != "recording" ? "Track-level vocal information is unknown; no vocal preference was inferred." : "Vocal preference uses explicit track-level descriptive evidence only.");
            return reasons;
        }

        IReadOnlyList<StoredAlbumFallback> Fallbacks(HydratedCandidateTrack c) => taste.AlbumFallbacks
            .Where(f => c.RawCandidate.Paths.Any(p => p.Seed == f.ArtistId.ToString("D"))).ToArray();
        double Influence(HydratedCandidateTrack c)
        {
            var fallback = Fallbacks(c);
            bool direct = c.RawCandidate.Paths.Any(p => !fallback.Any(f => p.Seed == f.ArtistId.ToString("D")) ||
                effective.PositiveSignals.Any(s => s.TargetType == LinerNotes.Domain.Enums.TasteTargetType.Artist && s.CanonicalEntityId?.ToString("D") == p.Seed));
            return direct || fallback.Count == 0 ? 1 : fallback.Max(f => f.Influence);
        }
    }

    private static IReadOnlyList<HydratedCandidateTrack> Merge(IEnumerable<HydratedCandidateTrack> candidates)
    {
        var groups = new Dictionary<List<HydratedCandidateTrack>, (HashSet<string> Aliases, int Order)>();
        var byAlias = new Dictionary<string, List<HydratedCandidateTrack>>(StringComparer.Ordinal);
        int order = 0;
        foreach (var candidate in candidates.OrderBy(c => c.Track.TrackKey, StringComparer.Ordinal))
        {
            var aliases = TrackIdentity.Aliases(candidate.Track);
            var matches = aliases.Where(byAlias.ContainsKey).Select(alias => byAlias[alias]).Distinct()
                .OrderBy(group => groups[group].Order).ToArray();
            var group = new List<HydratedCandidateTrack> { candidate };
            var groupAliases = new HashSet<string>(aliases, StringComparer.Ordinal);
            foreach (var match in matches)
            {
                group.AddRange(match); groupAliases.UnionWith(groups[match].Aliases); groups.Remove(match);
            }
            foreach (var alias in groupAliases) byAlias[alias] = group;
            groups.Add(group, (groupAliases, order++));
        }
        return groups.Keys.Select(g =>
        {
            var first = g.OrderByDescending(c => c.TagScope == "recording").ThenByDescending(c => c.TagVector.Weights.Count).ThenBy(c => c.Track.TrackKey, StringComparer.Ordinal).First();
            return new HydratedCandidateTrack(first.Track, first.TagVector,
                new(first.Track.Title, first.Track.ArtistName, first.Track.Mbid, g.SelectMany(c => c.RawCandidate.Paths).Distinct().ToArray()),
                first.Tags, first.TagResponse, first.TagMissingReason, g.SelectMany(c => c.Gaps).Distinct())
            {
                TagScope = first.TagScope,
                TagNormalizationVersion = first.TagNormalizationVersion,
                SupportingResponses = first.SupportingResponses
            };
        }).OrderBy(c => c.Track.TrackKey, StringComparer.Ordinal).ToArray();
    }
}
