using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Application.Features.Digests.Commands.GenerateDigest;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Taste;
using NSubstitute;
using Xunit;

namespace LinerNotes.Application.Tests.Features.Digests;

public sealed class GenerateDigestTests
{
    private readonly Guid userId = Guid.NewGuid();
    private readonly IDigestGenerationStore store = Substitute.For<IDigestGenerationStore>();
    private readonly IRecommendationSource source = Substitute.For<IRecommendationSource>();
    private readonly ICandidateHydrator hydrator = Substitute.For<ICandidateHydrator>();
    private readonly ISeedTagSource seeds = Substitute.For<ISeedTagSource>();
    private readonly IGenerationStorage storage = Substitute.For<IGenerationStorage>();
    private readonly GenerationConfiguration config = new() { RecordingExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(1),
        ApprovedRecordingManifestSha256 = new string('a', 64) };
    private GenerateDigestCommandHandler Handler => new(store, source, hydrator, seeds, storage, config, new());

    private void EmptyGeneration()
    {
        store.ReadInputsAsync(userId, Arg.Any<CancellationToken>()).Returns(new GenerationInputs(true,
            [TasteSignal.CreateSeedTag(userId, "rock", 1, "manual")], [], "rev"));
        store.ReadStorageAsync(Arg.Any<CancellationToken>()).Returns(new GenerationStorageState(1000, "db"));
        var lease = Substitute.For<IGenerationStorageLease>();
        var reservation = Substitute.For<IGenerationStorageReservation>();
        storage.AcquireAsync(Arg.Any<GenerationStorageState>(), Arg.Any<CancellationToken>()).Returns(lease);
        lease.ReserveAsync(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<GenerationStorageState>(), Arg.Any<CancellationToken>()).Returns(reservation);
        source.GetCandidatesByArtistsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new IngestionResult<RawCandidateTrack>([]));
        source.GetCandidatesByTagsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new IngestionResult<RawCandidateTrack>([]));
        hydrator.HydrateCandidatesBatchAsync(Arg.Any<IReadOnlyList<RawCandidateTrack>>(), Arg.Any<CancellationToken>()).Returns(new IngestionResult<HydratedCandidateTrack>([]));
        store.PersistAsync(userId, Arg.Any<IsoWeek>(), "rev", Arg.Any<IReadOnlyList<BaselineAPick>>(), Arg.Any<IGenerationStorageReservation>(), Arg.Any<CancellationToken>())
            .Returns(call => new WeeklyDigest(userId, call.Arg<IsoWeek>(), DateTime.UtcNow, DateTime.UtcNow.AddDays(7)));
    }

    private static RawCandidateTrack Candidate(string title = "Track", EvidenceOrigin origin = EvidenceOrigin.Recorded)
    {
        var response = new ResponseReference("Provider", "tracks", "rock", origin, new string('a', 64), DateTimeOffset.UnixEpoch);
        return new(title, "Artist", null, [new("rock", ObservedValue<double>.Missing(), null, response, 1,
            null, null, ObservedValue<long>.Missing())]);
    }

    [Fact]
    public async Task EmptySupportedDigest_IsPersistedWithCoverageDiagnostics()
    {
        EmptyGeneration();
        source.GetCandidatesByTagsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new IngestionResult<RawCandidateTrack>([], [new("rock", "tracks", "no_supported_tracks")]));
        var result = await Handler.Handle(new(userId, new(2026, 41)), default);
        Assert.Equal("generated", result.Status);
        Assert.Empty(result.Digest!.Recommendations);
        Assert.Contains(result.Gaps, gap => gap.Reason == "no_supported_tracks");
    }

    [Fact]
    public async Task CandidateOverflow_StopsBeforeHydrationAndLiveOriginIsRejected()
    {
        EmptyGeneration(); config.MaximumCandidates = 1;
        source.GetCandidatesByTagsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new IngestionResult<RawCandidateTrack>([Candidate("One"), Candidate("Two")]));
        Assert.Equal("candidate_limit", (await Handler.Handle(new(userId, new(2026, 41)), default)).Reason);
        await hydrator.DidNotReceiveWithAnyArgs().HydrateCandidatesBatchAsync(default!, default);
        source.GetCandidatesByTagsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new IngestionResult<RawCandidateTrack>([Candidate(origin: EvidenceOrigin.Live)]));
        Assert.Equal("origin_mismatch", (await Handler.Handle(new(userId, new(2026, 41)), default)).Reason);
    }

    [Fact]
    public async Task SnapshotOverflowAndCancellation_NeverPersistPartialResults()
    {
        EmptyGeneration(); config.MaximumSnapshotBytes = 1;
        var raw = Candidate();
        source.GetCandidatesByTagsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new IngestionResult<RawCandidateTrack>([raw]));
        hydrator.HydrateCandidatesBatchAsync(Arg.Any<IReadOnlyList<RawCandidateTrack>>(), Arg.Any<CancellationToken>())
            .Returns(new IngestionResult<HydratedCandidateTrack>([new(Track.Create(raw.Title, raw.ArtistName), WeightedTagVector.Empty,
                raw, [], raw.Paths[0].TrackResponse, "no_tags")]));
        Assert.Equal("snapshot_limit", (await Handler.Handle(new(userId, new(2026, 41)), default)).Reason);
        await store.DidNotReceiveWithAnyArgs().PersistAsync(default, default!, default!, default!, default!, default);
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Handler.Handle(new(userId, new(2026, 41)), cts.Token));
    }

    [Fact]
    public async Task ExistingDigest_RetryNeverQueriesProvidersOrStorage()
    {
        var week = new IsoWeek(2026, 41);
        var digest = new WeeklyDigest(userId, week, DateTime.UtcNow, DateTime.UtcNow.AddDays(7));
        store.GetExistingAsync(userId, week, Arg.Any<CancellationToken>()).Returns(digest);
        var result = await Handler.Handle(new(userId, week), default);
        Assert.Equal("existing", result.Status);
        Assert.Equal(digest.Id, result.Digest!.Id);
        await source.DidNotReceiveWithAnyArgs().GetCandidatesByArtistsAsync(default!, default, default);
        await seeds.DidNotReceiveWithAnyArgs().GetAsync(default!, default);
        await storage.DidNotReceiveWithAnyArgs().AcquireAsync(default!, default);
    }

    [Fact]
    public async Task ExpiredRecording_StopsNewGenerationBeforeProviders()
    {
        config.RecordingExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        Assert.Equal("recording_manifest_unconfigured_or_expired", (await Handler.Handle(new(userId, new(2026, 41)), default)).Reason);
        await source.DidNotReceiveWithAnyArgs().GetCandidatesByArtistsAsync(default!, default, default);
    }

    [Fact]
    public async Task RecordingExpiryDuringPersistence_FailsTheFinalReservationCheck()
    {
        EmptyGeneration();
        var clock = new TestClock();
        config.RecordingExpiresAtUtc = clock.Now.AddMinutes(1);
        store.PersistAsync(userId, Arg.Any<IsoWeek>(), "rev", Arg.Any<IReadOnlyList<BaselineAPick>>(),
            Arg.Any<IGenerationStorageReservation>(), Arg.Any<CancellationToken>()).Returns(async call =>
            {
                var reservation = call.Arg<IGenerationStorageReservation>();
                await reservation.ValidateAsync(new(1000, "db"));
                clock.Now += TimeSpan.FromMinutes(2);
                await reservation.VerifyStoredAsync(0, new(1000, "db"));
                return new WeeklyDigest(userId, call.Arg<IsoWeek>(), DateTime.UtcNow, DateTime.UtcNow.AddDays(7));
            });
        var handler = new GenerateDigestCommandHandler(store, source, hydrator, seeds, storage, config, new(), clock);
        Assert.Equal("recording_expired_before_persistence", (await handler.Handle(new(userId, new(2026, 41)), default)).Reason);
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task SeedOverflow_StopsBeforeProviderCalls()
    {
        var signals = Enumerable.Range(0, 21).Select(i => TasteSignal.CreateSeedArtist(userId, "artist" + i, 1, "manual")).ToArray();
        store.ReadInputsAsync(userId, Arg.Any<CancellationToken>()).Returns(new GenerationInputs(true, signals, [], "rev"));
        var result = await Handler.Handle(new(userId, new(2026, 41)), default);
        Assert.Equal("seed_limit", result.Reason);
        await seeds.DidNotReceiveWithAnyArgs().GetAsync(default!, default);
    }

    [Fact]
    public async Task InvalidIsoWeekAndNonfiniteSignals_AreStopped()
    {
        var invalidWeek = await Handler.Handle(new(userId, new(2025, 53)), default);
        Assert.Equal("invalid_iso_week", invalidWeek.Reason);
        store.ReadInputsAsync(userId, Arg.Any<CancellationToken>()).Returns(new GenerationInputs(true,
            [TasteSignal.CreateSeedTag(userId, "rock", double.NaN, "manual")], [], "rev"));
        Assert.Equal("invalid_generation_input", (await Handler.Handle(new(userId, new(2026, 41)), default)).Reason);
    }

    [Fact]
    public void LatestFeedback_RebuildsPositiveTasteAndKeepsFamiliarityAcrossAliases()
    {
        var track = Track.Create("Same", "Artist", mbid: "identifier");
        var old = new WeeklyRecommendation(Guid.NewGuid(), userId, track, 1,
            new ScoreBreakdown { MatchedTags = [new("rock", 1, 1, 1)] }, id: Guid.Parse("00000000-0000-0000-0000-000000000001"));
        old.RecordFeedback(UserFeedback.Liked, rating: 9);
        var latest = new WeeklyRecommendation(Guid.NewGuid(), userId, Track.Create("Same", "Artist"), 1, new ScoreBreakdown());
        latest.RecordFeedback(UserFeedback.Disliked, rating: 2);
        var staleLike = new TasteSignal(userId, TasteTargetType.Artist, "Artist", 1, TasteSignalSource.RecommendationLike, $"rec:{old.Id} - stale like");
        var known = new TasteSignal(userId, TasteTargetType.Track, track.TrackKey, 0, TasteSignalSource.RecommendationAlreadyKnown, $"rec:{old.Id} - familiar");
        var result = EffectiveTasteInputs.From(userId, [staleLike, known], [old, latest], new());
        Assert.Empty(result.PositiveSignals);
        Assert.Contains("artist:same", result.ExcludedAliases);
        Assert.Contains("mbid:identifier", result.ExcludedAliases);
        latest.RecordFeedback(UserFeedback.Liked, rating: 8);
        var changed = EffectiveTasteInputs.From(userId, [staleLike, known], [old, latest], new());
        Assert.Single(changed.PositiveSignals);
        Assert.Contains("artist:same", changed.ExcludedAliases);
        Assert.DoesNotContain("artist:sibling", changed.ExcludedAliases);
    }
}
