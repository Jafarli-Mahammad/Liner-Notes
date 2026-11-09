using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Mappings;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Discovery;
using LinerNotes.Domain.Taste;

namespace LinerNotes.Application.Features.Discovery;

public sealed record PreparedDiscoveryBatch(Guid Id, IReadOnlyList<BaselineAPick> Picks, string CoverageJson,
    DateTimeOffset? EvidenceExpiry, long ReservationBytes, IReadOnlyList<CoverageGap>? Gaps = null);

public sealed class PrepareDiscoveryBatch(MaterializeDiscoveryTaste materializer, IDiscoveryPreparation preparation,
    IDiscoveryBatchStore store, GenerationConfiguration configuration, BaselineAScorer scorer, IDiscoveryRequestBudget? requestBudget = null)
{
    public async Task<PreparedDiscoveryBatch> PrepareAsync(DiscoveryInputs inputs, Guid batchId, bool allowAcquisition, CancellationToken ct, LinerNotes.Domain.Digest.IsoWeek? week = null)
    {
        using var measurement = LinerNotes.Application.Common.Diagnostics.DiscoveryDiagnostics.Measure("preparation.total");
        var scoring = configuration.Scoring();
        using var acquisitionBudget = requestBudget?.Begin(configuration.MaximumUpstreamRequests);
        if (configuration.Origin == EvidenceOrigin.Recorded) throw new GenerationStoppedException("evaluation_evidence_ineligible");
        var effective = EffectiveTasteInputs.FromProfile(inputs.Profile.Id, inputs.Signals, inputs.Feedback,
            new(configuration.FeedbackTagWeight, configuration.MinimumPositiveFeedbackWeight, configuration.MinimumNegativeFeedbackRecordings), inputs.FeedbackHistory);
        var feedbackRecordings = effective.FeedbackRecordings;
        var taste = await materializer.BuildAsync(effective.PositiveSignals, allowAcquisition, ct, inputs.GenerationOrdinal, feedbackRecordings);
        var answers = OnboardingState.Read(inputs.Profile.OnboardingJson);
        var artistRequests = taste.Artists.Select(a => new DiscoveryPoolRequest(a.Id, a.Name, false))
            .Concat(taste.Recordings.Select(r => new DiscoveryPoolRequest(r.Id, r.Name, false, true)))
            .Concat(taste.LegacyArtists.Select(name => new DiscoveryPoolRequest(null, name, false)));
        int acquisitionLimit = configuration.Selection.AcquisitionLimit(answers, configuration.DiscoveryLimit);
        var requests = LinerNotes.Domain.Discovery.DiscoverySelectionPolicy.Window(artistRequests.ToArray(), configuration.MaximumArtistSeeds, inputs.GenerationOrdinal)
            .Concat(taste.Tags.Select(t => new DiscoveryPoolRequest(null, t, true))).ToArray();
        // Legacy imported artist seeds have no canonical catalog identity.
        if (answers?.AllStyles == true && (answers.Exploration == "surprise" || taste.Tags.Count == 0))
        {
            int slots = Math.Min(configuration.Selection.BroadTagSlots, configuration.MaximumTagSeeds);
            var broad = slots == 0 ? [] : DiscoverySelectionPolicy.Window(TasteVocabulary.Entries
                .Where(t => t.Category == "genre" && !taste.Tags.Contains(t.Id)).Select(t => t.Id).ToArray(), slots, inputs.GenerationOrdinal);
            int remaining = configuration.MaximumTagSeeds - broad.Count;
            var focused = remaining == 0 ? [] : DiscoverySelectionPolicy.Window(taste.Tags, remaining, inputs.GenerationOrdinal);
            requests = requests.Where(r => !r.IsTag).Concat(focused.Concat(broad).Select(t => new DiscoveryPoolRequest(null, t, true))).ToArray();
        }
        requests = requests.Select(r => r with { Limit = acquisitionLimit }).ToArray();
        if (requests.Length > configuration.MaximumArtistSeeds + configuration.MaximumTagSeeds)
            throw new GenerationStoppedException("seed_limit");
        var pools = new List<PreparedDiscoveryPool>();
        // Independent public pools have their own scopes/leases. Keep input order for evidence and tie-breaking.
        if (allowAcquisition)
        {
            using var parallelism = new SemaphoreSlim(4);
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
        var gaps = taste.Gaps.Concat(pools.SelectMany(p => p.Gaps)).Distinct().ToArray();
        if (pools.Sum(p => p.Candidates.Count) > configuration.MaximumCandidates) throw new GenerationStoppedException("candidate_limit");
        var hydrated = Merge(pools.SelectMany(p => p.Candidates));
        DiscoverySourceValidation.ThrowIfFailed(gaps.Concat(hydrated.SelectMany(candidate => candidate.Gaps)));
        if (hydrated.Count > configuration.MaximumCandidates) throw new GenerationStoppedException("candidate_limit");
        var aliases = hydrated.SelectMany(c => TrackIdentity.Aliases(c.Track)).Distinct(StringComparer.Ordinal).ToArray();
        var excluded = (await store.FindExcludedAsync(inputs.Profile.Id, aliases, ct)).Concat(effective.ExcludedAliases).ToHashSet(StringComparer.Ordinal);
        string inputHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(effective.PositiveSignals.OrderBy(s => s.NormalizedTargetValue, StringComparer.Ordinal).Select(s => new { s.TargetType, s.NormalizedTargetValue, s.Weight, s.CanonicalEntityId }))));
        string evidenceHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(taste.Seeds)));
        var materializedSignalIds = taste.Seeds.Where(s => s.SignalId.HasValue).Select(s => s.SignalId!.Value).ToHashSet();
        var receiptSeeds = taste.Seeds.Take(80).ToArray();
        var feedbackSupport = effective.FeedbackSupport.Take(80).ToArray();
        string? feedbackSupportHash = effective.FeedbackSupport.Count == 0 ? null : Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(effective.FeedbackSupport)));
        // Learned tags share their recommendation ID with the learned artist.
        // Only entity signals produce materialized seeds used for selection interests.
        var weights = effective.PositiveSignals
            .Where(s => s.TargetType is LinerNotes.Domain.Enums.TasteTargetType.Artist or LinerNotes.Domain.Enums.TasteTargetType.Album or LinerNotes.Domain.Enums.TasteTargetType.Track)
            .ToDictionary(s => s.Id, s => s.Weight);
        double maximumWeight = effective.PositiveSignals.Select(s => s.Weight).DefaultIfEmpty(1).Max();
        var interests = taste.Seeds.Where(s => s.SignalId.HasValue && s.Weights.Count > 0 && weights.ContainsKey(s.SignalId.Value))
            .Select(s => new SelectionInterest(s.EntityIdentity ?? s.ArtistName, LinerNotes.Domain.Catalog.WeightedTagVector.FromDictionary(s.Weights),
                s.Influence * Math.Min(1, weights[s.SignalId!.Value] / maximumWeight), s.ArtistName)).ToArray();
        IReadOnlyList<BaselineAPick> picks;
        SelectionReceipt selection;
        using (LinerNotes.Application.Common.Diagnostics.DiscoveryDiagnostics.Measure("preparation.evidence_ranking"))
        {
            var candidates = hydrated.Select(c => new BaselineACandidate(c.Track, c.TagVector,
                new ScoreEvidence(effective.PositiveSignals.Where(s => s.CanonicalEntityId != null ? materializedSignalIds.Contains(s.Id) : c.TagVector.Weights.ContainsKey(s.NormalizedTargetValue)).Take(80).Select(s => s.Store()).ToArray(), receiptSeeds,
                    c.Tags.Select(t => t.Store()).ToArray(), c.TagResponse.Store(),
                    c.RawCandidate.Paths.Select(p => p.Store()).ToArray(), gaps.Concat(c.Gaps).Distinct().Select(g => g.Store()).ToArray(),
                    c.TagScope, c.PopularityMissingReason)
                {
                    MaterializationVersion = DiscoveryTasteMaterializer.Version,
                    TagNormalizationVersion = c.TagNormalizationVersion,
                    SupportingResponses = c.SupportingResponses.Select(r => r.Store()).ToArray(),
                    TasteEvidenceSha256 = evidenceHash,
                    TasteEvidenceCount = taste.Seeds.Count,
                    FavoriteEvidence = taste.FavoriteEvidence,
                    FeedbackPolicy = EffectiveTasteInputs.FeedbackPolicy,
                    FeedbackSupport = feedbackSupport,
                    FeedbackSupportCount = effective.FeedbackSupport.Count,
                    FeedbackSupportSha256 = feedbackSupportHash,
                    FullInputSha256 = inputHash,
                    FullInputCount = effective.PositiveSignals.Count,
                    SelectionPolicy = ConnectedSelection.Version,
                    CreditedArtistIds = c.CreditedArtistIds,
                    SelectionAnswers = answers is null ? null : JsonSerializer.Serialize(answers),
                    SelectionReasons = [],
                    AcquisitionWindow = requests.Select(r => (r.IsRecording ? "recording:" : r.IsTag ? "tag:" : "artist:") + (r.ArtistId?.ToString("D") ?? r.Seed)).ToArray(),
                    AlbumFallbacks = Fallbacks(c),
                    SimilarityInfluence = Influence(c)
                })).ToArray();
            var familiar = effective.FamiliarArtists.Concat(taste.FamiliarArtists).ToHashSet(StringComparer.Ordinal);
            foreach (var candidate in hydrated.Where(c => c.CreditedArtistIds.Any(taste.FamiliarArtistIds.Contains)))
                familiar.Add(candidate.Track.NormalizedArtistName);
            var scored = (week is null
                ? scorer.ScoreForBatch(candidates, taste.Vector, familiar, batchId, scoring)
                : scorer.Score(candidates, taste.Vector, familiar, week, scoring)).ToArray();
            var recordingSeeds = requests.Where(r => r.IsRecording).Select(r => r.ArtistId!.Value.ToString("D")).ToHashSet(StringComparer.Ordinal);
            (picks, selection) = SelectDiscoveryResults.Apply(scored, hydrated, interests, recordingSeeds, excluded,
                configuration.Selection, answers, configuration.MaximumPicks, inputs.RecentArtists);
        }
        long bytes = 65_536;
        foreach (var pick in picks)
        {
            int length = ScoreBreakdownJson.SerializeToUtf8Bytes(pick.Breakdown).Length;
            if (length > configuration.MaximumSnapshotBytes)
            {
                var failure = new GenerationStoppedException("snapshot_limit");
                failure.Data["SnapshotBytes"] = length;
                failure.Data["SnapshotLimitBytes"] = configuration.MaximumSnapshotBytes;
                throw failure;
            }
            bytes = checked(bytes + length * 4L + 32_768);
        }
        string coverage = JsonSerializer.Serialize(new
        {
            Version = "discovery-coverage-v2",
            Selection = selection,
            Acquisition = requests.Select(r => new { r.ArtistId, r.Seed, r.IsTag, r.IsRecording, r.Limit }).ToArray(),
            SupportedPicks = picks.Count,
            RequestedPicks = configuration.MaximumPicks,
            CandidateCount = hydrated.Count,
            SuppressedCandidates = hydrated.Count(c => TrackIdentity.Aliases(c.Track).Any(excluded.Contains)),
            FeedbackConsidered = inputs.FeedbackHistory?.Count ?? inputs.Feedback.Count,
            FavoriteEvidence = taste.FavoriteEvidence,
            FeedbackPolicy = EffectiveTasteInputs.FeedbackPolicy,
            FeedbackSupport = feedbackSupport,
            FeedbackSupportCount = effective.FeedbackSupport.Count,
            FeedbackSupportSha256 = feedbackSupportHash,
            Gaps = gaps.Select(g => new { g.Method, g.Reason }).Distinct().Take(40).ToArray(),
            Origin = configuration.Origin.ToString()
        });
        DateTimeOffset? expiry = pools.Where(p => p.ExpiresAtUtc > p.PreparedAtUtc).Select(p => p.ExpiresAtUtc)
            .Append(taste.EvidenceExpiresAtUtc).Min();
        return new(batchId, picks, coverage, expiry, bytes, gaps);

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
                CreditedArtistIds = g.SelectMany(c => c.CreditedArtistIds).Distinct().Order().ToArray(),
                TagNormalizationVersion = first.TagNormalizationVersion,
                SupportingResponses = first.SupportingResponses
            };
        }).OrderBy(c => c.Track.TrackKey, StringComparer.Ordinal).ToArray();
    }
}
