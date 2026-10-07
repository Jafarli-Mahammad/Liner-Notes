using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Application.Tests.Common;
using LinerNotes.Infrastructure.RecommendationSources.LastFm;
using LinerNotes.Infrastructure.RecommendationSources.LastFm.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinerNotes.Infrastructure.Tests.RecommendationSources;

public sealed record PilotWeightConfiguration(string Version, ImmutableSortedDictionary<string, PilotWeights> Weights);
public sealed record PilotProtocol(string Version, string WeightsVersion, string MembershipHash, string ScoringDefinition,
    string NormalizationVersion, string StoplistHash, ImmutableSortedDictionary<string, PilotWeights> Weights,
    ImmutableArray<string> KnownTracks, ImmutableArray<string> DislikedTracks,
    string NoiseRule, string PopularityRule, string BlindRule, string AdequacyContract)
{
    public static PilotProtocol Initial()
    {
        // Embedded configuration only: no runtime file/credential discovery in the harness.
        using var stream = typeof(PilotProtocol).Assembly.GetManifestResourceStream("LinerNotes.Pilot.Weights.json")
            ?? throw new InvalidOperationException("Missing versioned pilot weights.");
        var configuration = JsonSerializer.Deserialize<PilotWeightConfiguration>(stream)
            ?? throw new InvalidOperationException("Invalid pilot weights.");
        if (configuration.Version != "pilot-weights-v1" || configuration.Weights is null ||
            !configuration.Weights.Keys.SequenceEqual(new[] { "A", "B", "C", "D" }, StringComparer.Ordinal) ||
            configuration.Weights.Any(p => new[] { p.Value.Tag, p.Value.Novelty, p.Value.Match }.Any(w => !double.IsFinite(w) || w < 0) ||
                (p.Key != "C" && p.Value.Match != 0))) throw new ArgumentException("Invalid versioned evaluation configuration.");
        return new("pilot-protocol-v1", configuration.Version, Phase2ProfileMembership.ApprovedHash,
        PilotScoring.Definition, LastFmTagEvidence.NormalizationVersion,
        PilotScoring.Hash(JsonSerializer.Serialize(LastFmTagEvidence.Stoplist)),
        configuration.Weights, [], [],
        "frozen existing stoplist, trimmed invariant case; all raw nonblank tags once per artist; ambiguous names listed, no tuning",
        PilotEvaluation.PopularityMeasure + "; missing/conflicting or >2^53 values unassessable; empirical midrank percentiles",
        "six founder profiles; deduplicated top-five union A-D; concealed reproducible SHA256 order; like=1 neutral=0 dislike=-1; " +
        "complete ratings only; sum/5 including empty slots; directional margin +0.20 and positive on 4/6; no promotion",
        "11 seeds attempted; >=8/11 five picks; each of six clusters >=10 tracks/3 artists; >=60 tracks/20 artists; " +
        ">=80% candidate artists two usable tags; <=30% raw stoplist noise; >=95% requests usable; 100% retained parse-or-gap/provenance; " +
        "yield/tag/integrity failure stops comparison; >=80% valid similarity paths for C; >=80% authentic listener coverage for popularity; " +
        "k=3/5; correctness/determinism/arithmetic gates; stability +/-5%,10% individual/joint; pilot-only IDF distinct artists; no tuning");
    }

    public void Validate()
    {
        // These untuned definitions were approved before pilot outcomes. Hashing a changed protocol does not adopt it.
        if (JsonSerializer.Serialize(this) != JsonSerializer.Serialize(Initial()))
            throw new ArgumentException("Protocol differs from the untuned Phase 4 definitions.");
    }
}
public sealed record PilotRecordedInput(string Method, string Artist, byte[] Body, string Sha256,
    DateTimeOffset RetrievedAtUtc, int Limit);
public sealed record PilotReplayInput(PilotProtocol Protocol, ImmutableArray<PilotSeed> Seeds,
    ImmutableArray<PilotRecordedInput> Responses, int Attempts, bool AcquisitionComplete,
    ImmutableArray<CoverageGap> LedgerGaps, string Label);
public sealed record PilotCandidateEvidence(RawCandidateTrack Raw, [property: JsonIgnore] ImmutableArray<TagObservation> Tags,
    ResponseReference TagResponse, string? TagMissingReason, string TagScope, string TagNormalizationVersion,
    ImmutableArray<CoverageGap> Gaps);
public sealed record PilotReplayReport(string Label, PilotProtocol Protocol, PilotShape Shape,
    ImmutableArray<PilotRequest> Requests, ImmutableArray<PilotArtist> Artists,
    ImmutableArray<PilotCandidateEvidence> Candidates, PilotEvaluationResult? Evaluation,
    string CorpusComposition, string AcquisitionScope);

/// <summary>Injected byte snapshots only. This bridge contains no HTTP/DI, credential lookup or file discovery.</summary>
public static class PilotReplay
{
    public static async Task<PilotReplayReport> BuildAsync(PilotReplayInput input, CancellationToken cancellationToken = default,
        bool compare = true)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Protocol);
        input.Protocol.Validate();
        if (input.Seeds.IsDefault || input.Responses.IsDefault || input.LedgerGaps.IsDefault ||
            input.Label is not (PilotEvaluation.Label or EvaluationMetrics.Label) || input.Seeds.Length != 11 ||
            input.Seeds.Any(s => string.IsNullOrWhiteSpace(s.Value)) ||
            input.Seeds.Any(s => s.Set is not ("founder" or "development")) || input.Attempts is < 0 or > 143 ||
            input.Responses.Length > input.Attempts)
            throw new ArgumentException("Invalid pilot label, partitions or bounded request count.");
        var locked = Phase2ProfileMembership.Locked();
        var approvedSeeds = locked.Profiles.Where(p => p.Set != EvaluationSet.HeldOut && p.Set != EvaluationSet.Synthetic)
            .SelectMany(p => p.Seeds).ToHashSet(StringComparer.Ordinal);
        if (input.Seeds.Any(s => !approvedSeeds.Contains(ProfileLock.Canonical(s.Value))) ||
            input.Seeds.Select(s => ProfileLock.Canonical(s.Value)).Distinct(StringComparer.Ordinal).Count() != 11)
            throw new ArgumentException("Pilot seed outside reviewed membership.");
        var founderSeeds = locked.Profiles.Where(p => p.Set == EvaluationSet.Founder).SelectMany(p => p.Seeds).ToHashSet(StringComparer.Ordinal);
        if (!input.Seeds.Where(s => s.Set == "founder").Select(s => ProfileLock.Canonical(s.Value)).ToHashSet(StringComparer.Ordinal).SetEquals(founderSeeds))
            throw new ArgumentException("All eight founder seeds are required.");
        if (input.Seeds.Where(s => s.Set == "founder").Any(s => !locked.Profiles.Any(p => p.Set == EvaluationSet.Founder &&
            p.Genre == s.Cluster && p.Seeds.Contains(ProfileLock.Canonical(s.Value), StringComparer.Ordinal))))
            throw new ArgumentException("Founder cluster membership differs from its lock.");
        if (input.Responses.Any(r => r.Method is not ("artist.getSimilar" or "artist.getTopTags" or "artist.getTopTracks")) ||
            input.Responses.Any(r => string.IsNullOrWhiteSpace(r.Artist)) ||
            input.Responses.Any(r => r.Limit != (r.Method == "artist.getTopTags" ? 15 : 5)) ||
            input.Responses.GroupBy(r => (r.Method, Artist: ProfileLock.Canonical(r.Artist))).Any(g => g.Count() != 1))
            throw new ArgumentException("Pilot only admits unique allowlisted recorder requests.");
        var snapshots = input.Responses.Select(r => new RecordedLastFmResponse(r.Method, r.Artist,
            r.Body, r.Sha256, r.RetrievedAtUtc, r.Limit)).ToImmutableArray();
        var requests = new List<PilotRequest>();
        var artists = new List<PilotArtist>();
        var gaps = input.LedgerGaps.ToList();
        foreach (var r in input.Responses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string method = r.Method.ToLowerInvariant();
            if (method == "artist.getsimilar")
            {
                var parsed = LastFmEvidenceParser.Parse<LastFmArtistSummary>(method, r.Artist, r.Body,
                    EvidenceOrigin.Recorded, r.RetrievedAtUtc, r.Limit);
                requests.Add(new(parsed.Reference, parsed.Count, parsed.Gaps,
                    parsed.Count(a => LastFmEvidenceParser.Match(a.Match).IsValid),
                    parsed.Count + parsed.Gaps.Count(g => g.Reason == "invalid_item_identity")));
                gaps.AddRange(parsed.Gaps);
            }
            else if (method == "artist.gettoptags")
            {
                var parsed = LastFmEvidenceParser.Parse<LastFmTagItem>(method, r.Artist, r.Body,
                    EvidenceOrigin.Recorded, r.RetrievedAtUtc, int.MaxValue);
                var bounded = LastFmEvidenceParser.Parse<LastFmTagItem>(method, r.Artist, r.Body,
                    EvidenceOrigin.Recorded, r.RetrievedAtUtc, 15);
                var raw = LastFmTagEvidence.Materialize(parsed);
                var normalized = LastFmTagEvidence.Materialize(bounded);
                artists.Add(new(r.Artist, normalized.Vector, raw.Observations, parsed.Reference));
                requests.Add(new(parsed.Reference, parsed.Count, parsed.Gaps));
                gaps.AddRange(raw.Gaps);
            }
            else
            {
                var parsed = LastFmEvidenceParser.Parse<LastFmTrackItem>(method, r.Artist, r.Body,
                    EvidenceOrigin.Recorded, r.RetrievedAtUtc, r.Limit);
                requests.Add(new(parsed.Reference, parsed.Count, parsed.Gaps)); gaps.AddRange(parsed.Gaps);
            }
        }
        artists = artists.OrderBy(a => ProfileLock.Canonical(a.Identity), StringComparer.Ordinal).ToList();
        var source = new LastFmRecommendationSource(new RecordedLastFmApiClient(snapshots), NullLogger<LastFmRecommendationSource>.Instance);
        var discovered = await source.GetCandidatesByArtistsAsync(input.Seeds.Select(s => s.Value).ToArray(), 5, cancellationToken);
        var hydrator = new LastFmCandidateHydrator(new RecordedLastFmApiClient(snapshots), NullLogger<LastFmCandidateHydrator>.Instance);
        var hydrated = await hydrator.HydrateCandidatesBatchAsync(discovered.Items, cancellationToken);
        gaps.AddRange(discovered.Gaps); gaps.AddRange(hydrated.Gaps);
        // Empty snapshots still trace to the missing request/gap; never substitute a generic seed tag.
        foreach (var seed in input.Seeds.Where(s => !artists.Any(a => ProfileLock.Canonical(a.Identity) == ProfileLock.Canonical(s.Value))))
        {
            var response = await new RecordedLastFmApiClient(snapshots).GetArtistTopTagsAsync(seed.Value, 15, cancellationToken);
            artists.Add(new(seed.Value, LastFmTagEvidence.Materialize(response).Vector, [], response.Reference));
            gaps.AddRange(response.Gaps);
        }
        var known = input.Protocol.KnownTracks.ToHashSet(StringComparer.Ordinal);
        var disliked = input.Protocol.DislikedTracks.ToHashSet(StringComparer.Ordinal);
        var attemptedSeeds = requests.Where(r => r.Reference.Method == "artist.getsimilar").Select(r => ProfileLock.Canonical(r.Reference.RequestIdentity))
            .Concat(input.LedgerGaps.Where(g => g.Method == "artist.getsimilar").Select(g => ProfileLock.Canonical(g.Seed))).ToHashSet(StringComparer.Ordinal);
        var shape = PilotEvaluation.Shape(input.Seeds, artists, hydrated.Items, requests, input.Attempts, gaps, known, disliked, input.Label, attemptedSeeds);
        if (!input.AcquisitionComplete)
            shape = shape with { CanCompare = false, Gates = shape.Gates.Add(new("acquisition-complete", 0, 1, 1, false)) };
        PilotEvaluationResult? comparison = null;
        if (shape.CanCompare && compare)
        {
            var profiles = locked.Profiles.Where(p => p.Set == EvaluationSet.Founder).Select(p => new PilotProfileInput(p,
                hydrated.Where(c => c.RawCandidate.Paths.Any(path => p.Seeds.Contains(ProfileLock.Canonical(path.Seed), StringComparer.Ordinal))).ToImmutableArray())).ToArray();
            comparison = PilotEvaluation.Compare(shape, profiles, artists, input.Protocol.Weights, known, disliked);
        }
        return new(input.Label, input.Protocol, shape, requests.ToImmutableArray(),
            artists.OrderBy(a => ProfileLock.Canonical(a.Identity), StringComparer.Ordinal).ToImmutableArray(),
            hydrated.Select(c => new PilotCandidateEvidence(c.RawCandidate, c.Tags, c.TagResponse, c.TagMissingReason,
                c.TagScope, c.TagNormalizationVersion, c.Gaps)).ToImmutableArray(), comparison,
            "Distinct received pilot artists only, including received empty vectors. Genre labels/balance not verified; no labels invented. No held-out statistics.",
            "Recorder retains five top tracks per artist; the shared discovery source uses the existing first-two bound. Upstream pool is popularity ordered; scoring is popularity-neutral.");
    }
}
