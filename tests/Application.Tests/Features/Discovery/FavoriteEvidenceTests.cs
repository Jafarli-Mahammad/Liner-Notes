using System.Diagnostics;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Catalog;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Discovery;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Taste;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace LinerNotes.Application.Tests.Features.Discovery;

public sealed class FavoriteEvidenceTests(ITestOutputHelper output)
{
    private Guid owner = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private static SeedTagSnapshot Snapshot(string name, string tag = "fixture sound") => new(name,
        WeightedTagVector.FromDictionary(new Dictionary<string, double> { [tag] = 1 }), [],
        new("fixture", "seed.tags", name, EvidenceOrigin.Synthetic, new string('a', 64), Now), null, [])
    { SharedUntilUtc = Now.AddHours(1) };
    private TasteSignal Favorite(int index, CatalogKind kind = CatalogKind.Artist, bool local = true, Guid? creditedArtist = null, DateTimeOffset? expiry = null)
    {
        var id = Guid.NewGuid();
        string scope = kind switch { CatalogKind.Artist => "artist", CatalogKind.Album => "release-group", _ => "recording" };
        var item = new CatalogItemDto(Guid.NewGuid(), id, kind, $"Fixture {index:D4}", "Fixture credit", null, null, [],
            "fixture", "Synthetic fixture", Guid.NewGuid(), CatalogOrigin.Synthetic,
            [new(creditedArtist ?? Guid.NewGuid(), "Fixture credit", 0)], "fixture", "fixture",
            local ? [new("dimension " + index % 8, 1, "synthetic-" + scope)] : [], Now, expiry ?? Now.AddHours(1), new string('a', 64));
        var signal = TasteSignal.ForProfile(owner, null, kind switch
        {
            CatalogKind.Artist => TasteTargetType.Artist,
            CatalogKind.Album => TasteTargetType.Album,
            _ => TasteTargetType.Track
        }, item.Name, 1,
            TasteSignalSource.InitialSeedManual, "Synthetic favorite");
        signal.SetCatalogSelection(item.Id, id, JsonSerializer.Serialize(new CatalogSelectionSnapshot("catalog-selection-v1", item)));
        return signal;
    }
    private static (MaterializeDiscoveryTaste Builder, IRecommendationProviderResolver Resolver) Builder(IDiscoverySeedPreparation? seeds = null)
    {
        var resolver = Substitute.For<IRecommendationProviderResolver>();
        resolver.Resolve().Returns(new RecommendationProvider("fixture", Substitute.For<IRecommendationSource>(),
            Substitute.For<ICandidateHydrator>(), Substitute.For<ISeedTagSource>()));
        return (new(resolver, new GenerationConfiguration { Origin = EvidenceOrigin.Synthetic }, new(), new Clock(), seeds), resolver);
    }

    [Theory]
    [InlineData(41)]
    [InlineData(205)]
    [InlineData(1000)]
    public async Task FreshSavedFavoritesAllContributeIndependentlyOfAcquisitionRound(int count)
    {
        var signals = Enumerable.Range(0, count).Select(i => Favorite(i, new[] { CatalogKind.Artist, CatalogKind.Album, CatalogKind.Track }[i % 3])).ToArray();
        var (builder, _) = Builder();
        var watch = Stopwatch.StartNew();
        var first = await builder.BuildAsync(signals, false, default, round: 0);
        var later = await builder.BuildAsync(signals.Reverse().ToArray(), false, default, round: 17);
        output.WriteLine($"{count} favorites, two materializations: {watch.Elapsed.TotalMilliseconds:F2} ms");
        Assert.Equal(count, first.FavoriteEvidence.Count);
        Assert.All(first.FavoriteEvidence, state => Assert.Equal("supported", state.State));
        Assert.Equal(count, first.Seeds.Count);
        Assert.Equal(count, first.Vector.Weights.Values.Sum(), 8);
        Assert.Equal(first.Vector, later.Vector);
        Assert.Equal(first.FamiliarArtists.Order(), later.FamiliarArtists.Order());
    }

    [Fact]
    public async Task ColdAcquisitionIsBoundedWhilePreviouslyPreparedFavoritesRemainInTaste()
    {
        var signals = Enumerable.Range(0, 41).Select(i => Favorite(i, local: false)).ToArray();
        var cached = new Dictionary<Guid, SeedTagSnapshot>();
        var seeds = Substitute.For<IDiscoverySeedPreparation>();
        seeds.GetAsync(Arg.Any<DiscoverySeedRequest>(), false, Arg.Any<CancellationToken>()).Returns(call =>
            cached.TryGetValue(call.Arg<DiscoverySeedRequest>().EntityId!.Value, out var hit) ? hit : throw new DiscoveryPreparationRequiredException());
        int calls = 0;
        seeds.GetAsync(Arg.Any<DiscoverySeedRequest>(), true, Arg.Any<CancellationToken>()).Returns(call =>
        {
            calls++;
            var request = call.Arg<DiscoverySeedRequest>();
            return cached[request.EntityId!.Value] = Snapshot(request.Name);
        });
        var (builder, _) = Builder(seeds);
        var first = await builder.BuildAsync(signals, true, default, round: 0);
        Assert.Equal(20, calls);
        Assert.Equal(20, first.Seeds.Count);
        Assert.Equal(21, first.FavoriteEvidence.Count(s => s.State == "awaiting"));
        var next = await builder.BuildAsync(signals, true, default, round: 1);
        Assert.Equal(40, calls);
        Assert.Equal(40, next.Seeds.Count);
        Assert.Equal(40, next.Vector.Weights.Values.Sum());
        await builder.BuildAsync(signals, true, default, round: 2);
        Assert.Equal(41, calls);
        var prepared = await builder.BuildAsync(signals, false, default, round: 19);
        Assert.Equal(41, prepared.Seeds.Count);
        Assert.Equal(41, calls);
    }

    [Fact]
    public async Task DifferentRecordingHintsForOneFeedbackArtistCannotExceedPrimaryRequestBudget()
    {
        var manual = Enumerable.Range(0, 5).Select(i => TasteSignal.ForProfile(owner, null, TasteTargetType.Artist,
            "Manual fixture " + i, 1, TasteSignalSource.InitialSeedManual, "Fixture"));
        var feedback = Enumerable.Range(0, 20).Select(i => new TasteSignal(owner, TasteTargetType.Artist,
            "Shared fixture artist", 1, TasteSignalSource.RecommendationLike, "Fixture", Guid.NewGuid())).ToArray();
        var hints = feedback.ToDictionary(s => s.Id, _ => Guid.NewGuid());
        var seeds = Substitute.For<IDiscoverySeedPreparation>();
        seeds.GetAsync(Arg.Any<DiscoverySeedRequest>(), false, Arg.Any<CancellationToken>()).Returns<SeedTagSnapshot>(_ => throw new DiscoveryPreparationRequiredException());
        seeds.GetAsync(Arg.Any<DiscoverySeedRequest>(), true, Arg.Any<CancellationToken>()).Returns(call => Snapshot(call.Arg<DiscoverySeedRequest>().Name));
        var result = await Builder(seeds).Builder.BuildAsync(manual.Concat(feedback).ToArray(), true, default, feedbackRecordings: hints);
        Assert.Equal(20, result.Seeds.Count);
        Assert.Equal(5, result.FavoriteEvidence.Count(s => s.State == "awaiting"));
        await seeds.Received(20).GetAsync(Arg.Any<DiscoverySeedRequest>(), true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExpiredSavedEvidenceIsNotUsedOutsideAcquisitionWindow()
    {
        var signals = Enumerable.Range(0, 41).Select(i => Favorite(i, expiry: Now.AddSeconds(-1))).ToArray();
        var seeds = Substitute.For<IDiscoverySeedPreparation>();
        seeds.GetAsync(Arg.Any<DiscoverySeedRequest>(), false, Arg.Any<CancellationToken>()).Returns<SeedTagSnapshot>(_ => throw new DiscoveryPreparationRequiredException());
        seeds.GetAsync(Arg.Any<DiscoverySeedRequest>(), true, Arg.Any<CancellationToken>()).Returns(call => Snapshot(call.Arg<DiscoverySeedRequest>().Name, "fresh"));
        var result = await Builder(seeds).Builder.BuildAsync(signals, true, default);
        Assert.Single(result.Vector.Weights);
        Assert.Equal(20, result.Vector["fresh"]);
        Assert.Equal(21, result.FavoriteEvidence.Count(s => s.State == "awaiting"));
    }

    [Fact]
    public async Task AlbumFallbacksReuseCanonicalEvidenceWithoutMultiplyingItsInfluence()
    {
        var artist = Guid.NewGuid();
        var albums = Enumerable.Range(0, 41).Select(i => Favorite(i, CatalogKind.Album, local: false, creditedArtist: artist)).ToArray();
        var seeds = Substitute.For<IDiscoverySeedPreparation>();
        seeds.GetAsync(Arg.Any<DiscoverySeedRequest>(), false, Arg.Any<CancellationToken>()).Returns(call =>
        {
            var request = call.Arg<DiscoverySeedRequest>();
            return request.IsAlbum ? Snapshot(request.Name) with { Vector = WeightedTagVector.Empty, MissingReason = "tags_missing" } : Snapshot(request.Name);
        });
        var result = await Builder(seeds).Builder.BuildAsync(albums, false, default);
        Assert.Equal(.35, result.Vector["fixture sound"], 10);
        Assert.All(result.FavoriteEvidence, state => Assert.Equal("artist-fallback", state.State));
        await seeds.Received(1).GetAsync(Arg.Is<DiscoverySeedRequest>(r => r.EntityId == artist && !r.IsAlbum), false, Arg.Any<CancellationToken>());
        await seeds.DidNotReceive().GetAsync(Arg.Any<DiscoverySeedRequest>(), true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ColdAlbumAndCreditedArtistAcquisitionHaveSeparateBoundedWindows()
    {
        var albums = Enumerable.Range(0, 41).Select(i => Favorite(i, CatalogKind.Album, local: false)).ToArray();
        var seeds = Substitute.For<IDiscoverySeedPreparation>();
        var cache = new Dictionary<DiscoverySeedRequest, SeedTagSnapshot>();
        seeds.GetAsync(Arg.Any<DiscoverySeedRequest>(), false, Arg.Any<CancellationToken>()).Returns(call =>
            cache.TryGetValue(call.Arg<DiscoverySeedRequest>(), out var hit) ? hit : throw new DiscoveryPreparationRequiredException());
        int primary = 0, fallback = 0;
        seeds.GetAsync(Arg.Any<DiscoverySeedRequest>(), true, Arg.Any<CancellationToken>()).Returns(call =>
        {
            var request = call.Arg<DiscoverySeedRequest>();
            if (request.IsAlbum) primary++; else fallback++;
            return cache[request] = request.IsAlbum ? Snapshot(request.Name) with { Vector = WeightedTagVector.Empty, MissingReason = "tags_missing" } : Snapshot(request.Name);
        });
        var result = await Builder(seeds).Builder.BuildAsync(albums, true, default);
        Assert.Equal(20, primary);
        Assert.Equal(20, fallback);
        Assert.Equal(41, result.FavoriteEvidence.Count);
        Assert.Equal(20, result.FavoriteEvidence.Count(s => s.State == "artist-fallback"));
        Assert.Equal(21, result.FavoriteEvidence.Count(s => s.State == "awaiting"));
    }

    [Theory]
    [InlineData(102, 25, 7, 1)]
    [InlineData(1000, 0, 1, 1)]
    [InlineData(1000, 0, 1, 400)]
    public async Task ArtistRecordingAndLegacyAcquisitionShareOneFairWindowAndReceiptRemainsBounded(int favorites, int legacyCount, int rounds, int candidateCount)
    {
        var profile = ListenerProfile.Guest(Now, TimeSpan.FromDays(7));
        owner = profile.Id;
        var signals = Enumerable.Range(0, favorites).Select(i => Favorite(i, i % 2 == 0 ? CatalogKind.Artist : CatalogKind.Track)).ToList();
        signals.AddRange(Enumerable.Range(0, legacyCount).Select(i => TasteSignal.ForProfile(owner, null, TasteTargetType.Artist,
            $"Legacy fixture {i:D4}", 1, TasteSignalSource.InitialSeedManual, "Synthetic legacy favorite")));
        var seedPreparation = Substitute.For<IDiscoverySeedPreparation>();
        seedPreparation.GetAsync(Arg.Any<DiscoverySeedRequest>(), false, Arg.Any<CancellationToken>()).Returns(call => Snapshot(call.Arg<DiscoverySeedRequest>().Name));
        var (materializer, _) = Builder(seedPreparation);
        var preparation = Substitute.For<IDiscoveryPreparation>();
        var requested = new HashSet<string>();
        var track = Track.Create("Fixture candidate", "Unlisted fixture artist");
        var response = new ResponseReference("fixture", "tags", "fixture", EvidenceOrigin.Synthetic, new string('a', 64), Now);
        var candidate = new HydratedCandidateTrack(track, WeightedTagVector.FromDictionary(new Dictionary<string, double> { ["dimension 1"] = 1 }),
            new(track.Title, track.ArtistName, null, [new("fixture", new(null, null, "not_supplied"), null, response, 1, null, null, new(null, null, "not_supplied"))]), [], response, null);
        var varied = Enumerable.Range(0, candidateCount).Select(i =>
        {
            var row = Track.Create("Fixture " + i, "Independent fixture " + i, mbid: Guid.NewGuid().ToString("D"));
            return new HydratedCandidateTrack(row, candidate.TagVector, new(row.Title, row.ArtistName, row.Mbid, candidate.RawCandidate.Paths), [], response, null)
            { CreditedArtistIds = [Guid.NewGuid()] };
        }).ToArray();
        int poolIndex = 0;
        preparation.GetAsync(Arg.Any<DiscoveryPoolRequest>(), false, Arg.Any<CancellationToken>()).Returns(call =>
        {
            var request = call.Arg<DiscoveryPoolRequest>();
            requested.Add(request.ArtistId?.ToString() ?? request.Seed);
            var rows = candidateCount == 1 ? [candidate] : varied.Skip(poolIndex++ * 20).Take(20).ToArray();
            return new PreparedDiscoveryPool(new string('a', 64), "fixture", EvidenceOrigin.Synthetic, Now, Now.AddMinutes(10), "fixture", rows, [], 100, new string('b', 64));
        });
        var store = Substitute.For<IDiscoveryBatchStore>();
        store.FindExcludedAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>()).Returns(new HashSet<string>());
        var owned = signals.ToArray();
        var runner = new PrepareDiscoveryBatch(materializer, preparation, store, new() { Origin = EvidenceOrigin.Synthetic }, new());
        for (int round = 0; round < rounds; round++)
        {
            var result = await runner.PrepareAsync(new(profile, owned, [], round), Guid.NewGuid(), false, default);
            var receipt = result.Picks.First().Breakdown;
            Assert.Equal(candidateCount == 1 ? 1 : 5, result.Picks.Count);
            if (candidateCount == 400) Assert.Equal(400, receipt.Snapshot!.Evidence.Selection!.Input.Candidates.Count);
            Assert.Equal(favorites + legacyCount, receipt.Snapshot!.Evidence.TasteEvidenceCount);
            Assert.Equal(80, receipt.Snapshot.Evidence.Seeds.Count);
            Assert.Equal(favorites + legacyCount, receipt.Snapshot.Evidence.FavoriteEvidence.Count);
            var encoded = LinerNotes.Application.Common.Mappings.ScoreBreakdownJson.SerializeToUtf8Bytes(receipt);
            Assert.InRange(encoded.Length, 1, 100_000);
            if (candidateCount == 400)
            {
                var restored = LinerNotes.Application.Common.Mappings.ScoreBreakdownJson.Deserialize(System.Text.Encoding.UTF8.GetString(encoded));
                Assert.True(restored.Snapshot!.Evidence.Selection!.SelectedDecisionsOnly);
                Assert.Equal(400, LinerNotes.Domain.Discovery.ConnectedSelection.Replay(restored.Snapshot.Evidence.Selection).Decisions.Count);
                output.WriteLine($"ThousandFavoriteFourHundredCandidateStoredBytes={encoded.Length}");
                int coverageBytes = System.Text.Encoding.UTF8.GetByteCount(result.CoverageJson);
                output.WriteLine($"ThousandFavoriteFourHundredCandidateCoverageBytes={coverageBytes}");
                Assert.InRange(coverageBytes, 1, LinerNotes.Domain.Discovery.DiscoveryBatch.MaximumCoverageBytes);
            }
            Assert.Equal(receipt.FinalScore, new BaselineAScorer().Replay(receipt.Snapshot).FinalScore, 12);
            Assert.Equal(20, receipt.Snapshot.Evidence.AcquisitionWindow.Count);
        }
        Assert.Equal(rounds == 1 ? 20 : favorites + legacyCount, requested.Count);
    }
}
