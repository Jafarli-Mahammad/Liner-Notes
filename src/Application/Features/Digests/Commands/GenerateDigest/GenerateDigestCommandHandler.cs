using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Mappings;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Taste;
using MediatR;

namespace LinerNotes.Application.Features.Digests.Commands.GenerateDigest;

public sealed class GenerateDigestCommandHandler(IDigestGenerationStore store, IRecommendationSource source,
    ICandidateHydrator hydrator, ISeedTagSource seedTags, IGenerationStorage storage, GenerationConfiguration config,
    BaselineAScorer scorer, TimeProvider? timeProvider = null) : IRequestHandler<GenerateDigestCommand, GenerateDigestResult>
{
    public async Task<GenerateDigestResult> Handle(GenerateDigestCommand request, CancellationToken cancellationToken)
    {
        var gaps = new List<CoverageGap>();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.UserId == Guid.Empty || request.Week is null) throw new GenerationStoppedException("invalid_user_or_week");
            var scoring = config.Scoring();
            if (request.Week.WeekNumber > System.Globalization.ISOWeek.GetWeeksInYear(request.Week.Year))
                throw new GenerationStoppedException("invalid_iso_week");
            await using var batch = await store.AcquireBatchAsync(cancellationToken).ConfigureAwait(false);
            var existing = await store.GetExistingAsync(request.UserId, request.Week, cancellationToken).ConfigureAwait(false);
            if (existing is not null) return new("existing", existing.ToDto(), []);
            if (config.Origin == EvidenceOrigin.Recorded &&
                (config.RecordingExpiresAtUtc is not { } expiry || expiry <= (timeProvider ?? TimeProvider.System).GetUtcNow() ||
                 config.ApprovedRecordingManifestSha256 is not { Length: 64 } digestHash ||
                 !digestHash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f')))
                throw new GenerationStoppedException("recording_manifest_unconfigured_or_expired");
            var inputs = await store.ReadInputsAsync(request.UserId, cancellationToken).ConfigureAwait(false);
            if (!inputs.UserExists) return new("stopped", null, [], "user_not_found");
            var effective = EffectiveTasteInputs.From(request.UserId, inputs.Signals, inputs.Feedback,
                new(config.FeedbackTagWeight, config.MinimumPositiveFeedbackWeight));
            var artists = effective.PositiveSignals.Where(s => s.TargetType == TasteTargetType.Artist)
                .Select(s => s.NormalizedTargetValue).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var tags = effective.PositiveSignals.Where(s => s.TargetType == TasteTargetType.Tag)
                .Select(s => s.NormalizedTargetValue).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            if (artists.Length > config.MaximumArtistSeeds || tags.Length > config.MaximumTagSeeds)
                throw new GenerationStoppedException("seed_limit");
            var database = await store.ReadStorageAsync(cancellationToken).ConfigureAwait(false);
            await using var lease = await storage.AcquireAsync(database, cancellationToken).ConfigureAwait(false);
            var seeds = new List<SeedTagSnapshot>();
            foreach (var artist in artists)
            {
                var seed = await seedTags.GetAsync(artist, cancellationToken).ConfigureAwait(false);
                CheckOrigin(seed.Tags.Select(t => t.Response).Append(seed.Response), seed.Gaps);
                seeds.Add(seed); gaps.AddRange(seed.Gaps);
            }
            var taste = TasteVectorMaterializer.Build(effective.PositiveSignals,
                seeds.ToDictionary(s => s.ArtistName, s => s.Vector, StringComparer.Ordinal));
            var byArtist = await source.GetCandidatesByArtistsAsync(artists, config.DiscoveryLimit, cancellationToken).ConfigureAwait(false);
            var byTag = await source.GetCandidatesByTagsAsync(tags, config.DiscoveryLimit, cancellationToken).ConfigureAwait(false);
            gaps.AddRange(byArtist.Gaps); gaps.AddRange(byTag.Gaps);
            CheckOrigin(byArtist.Concat(byTag).SelectMany(c => c.Paths).SelectMany(p =>
                p.SimilarityResponse is null ? new[] { p.TrackResponse } : new[] { p.TrackResponse, p.SimilarityResponse }), gaps);
            var merged = MergeCandidates(byArtist.Concat(byTag));
            if (merged.Count > config.MaximumCandidates) throw new GenerationStoppedException("candidate_limit");
            var hydrated = await hydrator.HydrateCandidatesBatchAsync(merged, cancellationToken).ConfigureAwait(false);
            gaps.AddRange(hydrated.Gaps);
            CheckOrigin(hydrated.SelectMany(c => c.Tags.Select(t => t.Response).Append(c.TagResponse)), gaps);
            var candidates = hydrated.Select(c => new BaselineACandidate(c.Track, c.TagVector,
                new(effective.PositiveSignals.Select(s => s.Store()).ToArray(), seeds.Select(s => s.Store()).ToArray(),
                    c.Tags.Select(t => t.Store()).ToArray(), c.TagResponse.Store(),
                    c.RawCandidate.Paths.Select(p => p.Store()).ToArray(), gaps.Concat(c.Gaps).Distinct().Select(g => g.Store()).ToArray(),
                    c.TagScope, c.PopularityMissingReason))).ToArray();
            var picks = scorer.Rank(candidates, taste, effective.FamiliarArtists,
                effective.ExcludedAliases, request.Week, scoring, config.MaximumPicks);
            long bytes = 0;
            foreach (var pick in picks)
            {
                int length = JsonSerializer.SerializeToUtf8Bytes(pick.Breakdown).Length;
                if (length > config.MaximumSnapshotBytes) throw new GenerationStoppedException("snapshot_limit");
                bytes = checked(bytes + length);
            }
            var reservation = await lease.ReserveAsync(bytes, picks.Count, await store.ReadStorageAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
            var guardedReservation = new ExpiryReservation(reservation,
                config.Origin == EvidenceOrigin.Recorded ? config.RecordingExpiresAtUtc : null, timeProvider ?? TimeProvider.System);
            var digest = await store.PersistAsync(request.UserId, request.Week, inputs.Revision, picks, guardedReservation, cancellationToken).ConfigureAwait(false);
            return new("generated", digest.ToDto(), gaps.Distinct().ToArray());
        }
        catch (GenerationStoppedException ex) { return new("stopped", null, gaps.Distinct().ToArray(), ex.Message); }
        catch (ArgumentException) { return new("stopped", null, gaps.Distinct().ToArray(), "invalid_generation_input"); }
    }

    private void CheckOrigin(IEnumerable<ResponseReference> references, IEnumerable<CoverageGap> gaps)
    {
        if (references.Concat(gaps.Where(g => g.Response != null).Select(g => g.Response!)).Any(r => r.Origin != config.Origin))
            throw new GenerationStoppedException("origin_mismatch");
    }

    private sealed class ExpiryReservation(IGenerationStorageReservation inner, DateTimeOffset? expiresAt, TimeProvider clock)
        : IGenerationStorageReservation
    {
        public async Task ValidateAsync(GenerationStorageState database, CancellationToken cancellationToken = default)
        {
            CheckExpiry();
            await inner.ValidateAsync(database, cancellationToken).ConfigureAwait(false);
            CheckExpiry();
        }
        public async Task VerifyStoredAsync(long actualColumnBytes, GenerationStorageState database, CancellationToken cancellationToken = default)
        {
            CheckExpiry();
            await inner.VerifyStoredAsync(actualColumnBytes, database, cancellationToken).ConfigureAwait(false);
            CheckExpiry();
        }
        private void CheckExpiry()
        {
            if (expiresAt <= clock.GetUtcNow()) throw new GenerationStoppedException("recording_expired_before_persistence");
        }
    }

    private static IReadOnlyList<RawCandidateTrack> MergeCandidates(IEnumerable<RawCandidateTrack> candidates)
    {
        var groups = new List<List<RawCandidateTrack>>();
        foreach (var candidate in candidates.OrderBy(c => c.CandidateKey, StringComparer.Ordinal))
        {
            var aliases = TrackIdentity.Aliases(Track.Create(candidate.Title, candidate.ArtistName, mbid: candidate.Mbid));
            var matches = groups.Where(g => g.Any(c => TrackIdentity.Aliases(Track.Create(c.Title, c.ArtistName, mbid: c.Mbid)).Intersect(aliases, StringComparer.Ordinal).Any())).ToArray();
            var group = new List<RawCandidateTrack> { candidate };
            foreach (var match in matches) { group.AddRange(match); groups.Remove(match); }
            groups.Add(group);
        }
        return groups.Select(g =>
        {
            var first = g.OrderByDescending(c => c.Mbid != null).ThenBy(c => c.CandidateKey, StringComparer.Ordinal).First();
            return new RawCandidateTrack(first.Title, first.ArtistName, first.Mbid, g.SelectMany(c => c.Paths).Distinct());
        }).OrderBy(c => c.CandidateKey, StringComparer.Ordinal).ToArray();
    }
}
