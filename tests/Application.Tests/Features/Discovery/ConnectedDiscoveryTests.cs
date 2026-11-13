using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Catalog;
using LinerNotes.Application.Common.Interfaces.Discovery;
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

namespace LinerNotes.Application.Tests.Features.Discovery;

public sealed class ConnectedDiscoveryTests
{
    [Theory]
    [InlineData("close", 3, false)]
    [InlineData("balance", 6, false)]
    [InlineData("surprise", 10, false)]
    [InlineData("surprise", 10, true)]
    public async Task AcquisitionChangesPublicLimitsAndPermittedRoutesWithoutChangingTaste(string exploration, int limit, bool allStyles)
    {
        var profile = ListenerProfile.Guest(DateTimeOffset.UtcNow, TimeSpan.FromDays(7));
        var answers = new TasteOnboarding(Goal: "favorites", Exploration: exploration, AllStyles: allStyles);
        profile.SetOnboarding(JsonSerializer.Serialize(answers));
        var signal = TasteSignal.ForProfile(profile.Id, null, TasteTargetType.Tag, "electronic", 1, TasteSignalSource.InitialSeedManual, "fixture");
        var requests = new List<DiscoveryPoolRequest>();
        var preparation = Substitute.For<IDiscoveryPreparation>();
        preparation.GetAsync(Arg.Any<DiscoveryPoolRequest>(), true, Arg.Any<CancellationToken>()).Returns(call =>
        {
            lock (requests) requests.Add(call.Arg<DiscoveryPoolRequest>());
            return new PreparedDiscoveryPool("fixture", "fixture", EvidenceOrigin.Synthetic, DateTimeOffset.UtcNow, null, "fixture", [], [], 0, "fixture");
        });
        var resolver = Substitute.For<IRecommendationProviderResolver>();
        resolver.Resolve().Returns(new RecommendationProvider("fixture", Substitute.For<IRecommendationSource>(), Substitute.For<ICandidateHydrator>(), Substitute.For<ISeedTagSource>()));
        var store = Substitute.For<IDiscoveryBatchStore>();
        store.FindExcludedAsync(profile.Id, Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>()).Returns(new HashSet<string>());
        var config = new GenerationConfiguration { Origin = EvidenceOrigin.Synthetic };
        var runner = new PrepareDiscoveryBatch(new(resolver, config, new(), TimeProvider.System), preparation, store, config, new());
        var result = await runner.PrepareAsync(new(profile, [signal], []), Guid.NewGuid(), true, default);
        Assert.All(requests, r => Assert.Equal(limit, r.Limit));
        Assert.Contains(requests, r => r.Seed == "electronic");
        Assert.Equal(allStyles ? 4 : 1, requests.Count);
        Assert.Empty(result.Picks);
        using var coverage = JsonDocument.Parse(result.CoverageJson);
        var receipt = coverage.RootElement.GetProperty("Selection").Deserialize<SelectionReceipt>()!;
        Assert.Equal(5, receipt.Shortfall);
        ConnectedSelection.Replay(receipt);
        Assert.Equal(answers, receipt.Input.Answers);
    }

    [Fact]
    public async Task AnActualFavoriteRecordingPathSurvivesWithoutTagsAndReplaysFromItsPrivateReceipt()
    {
        var profile = ListenerProfile.Guest(DateTimeOffset.UtcNow, TimeSpan.FromDays(7));
        var recording = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var item = new CatalogItemDto(Guid.NewGuid(), recording, CatalogKind.Track, "Favorite fixture", "Fixture credit", null, null, [],
            "fixture", "fixture", Guid.NewGuid(), CatalogOrigin.Synthetic, [new(Guid.NewGuid(), "Fixture credit", 0)], "fixture", "fixture",
            [new("electronic", 1, "synthetic-recording")], now, now.AddHours(1), new string('a', 64));
        var signal = TasteSignal.ForProfile(profile.Id, null, TasteTargetType.Track, item.Name, 1, TasteSignalSource.InitialSeedManual, "fixture");
        signal.SetCatalogSelection(item.Id, recording, JsonSerializer.Serialize(new CatalogSelectionSnapshot("catalog-selection-v1", item)));
        var reference = new ResponseReference("fixture", "recording.metadata", recording.ToString("D"), EvidenceOrigin.Synthetic, new string('b', 64), now);
        var track = Track.Create("Connected fixture", "Independent fixture", mbid: Guid.NewGuid().ToString("D"));
        var candidate = new HydratedCandidateTrack(track, WeightedTagVector.Empty, new(track.Title, track.ArtistName, track.Mbid,
            [new(recording.ToString("D"), ObservedValue<double>.Missing("not_supplied"), reference, reference, 1, null, null, ObservedValue<long>.Missing("not_supplied"))]), [], reference, "missing")
        { TagScope = "none", CreditedArtistIds = [item.Artists![0].CanonicalId] };
        var resolver = Substitute.For<IRecommendationProviderResolver>();
        resolver.Resolve().Returns(new RecommendationProvider("fixture", Substitute.For<IRecommendationSource>(), Substitute.For<ICandidateHydrator>(), Substitute.For<ISeedTagSource>()));
        var preparation = Substitute.For<IDiscoveryPreparation>();
        preparation.GetAsync(Arg.Any<DiscoveryPoolRequest>(), true, Arg.Any<CancellationToken>()).Returns(
            new PreparedDiscoveryPool("fixture", "fixture", EvidenceOrigin.Synthetic, now, now.AddHours(1), "fixture", [candidate], [], 1000, "fixture"));
        var store = Substitute.For<IDiscoveryBatchStore>();
        store.FindExcludedAsync(profile.Id, Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>()).Returns(new HashSet<string>());
        var config = new GenerationConfiguration { Origin = EvidenceOrigin.Synthetic };
        var runner = new PrepareDiscoveryBatch(new(resolver, config, new(), TimeProvider.System), preparation, store, config, new());
        var result = await runner.PrepareAsync(new(profile, [signal], []), Guid.NewGuid(), true, default);
        var pick = Assert.Single(result.Picks);
        Assert.Equal(0, pick.Breakdown.NoveltyRaw);
        Assert.True(pick.Breakdown.Snapshot!.Familiarity);
        Assert.Equal(0, pick.Breakdown.TagSimilarityRaw);
        Assert.Equal(0, pick.Breakdown.TagSimilarityScore);
        var stored = LinerNotes.Application.Common.Mappings.ScoreBreakdownJson.Deserialize(LinerNotes.Application.Common.Mappings.ScoreBreakdownJson.Serialize(pick.Breakdown));
        var receipt = stored.Snapshot!.Evidence.Selection!;
        Assert.Equal(recording.ToString("D"), Assert.Single(receipt.Input.Candidates).RecordingSeed);
        Assert.Equal(4, receipt.Shortfall);
        Assert.Equal(track.TrackKey, Assert.Single(ConnectedSelection.Replay(receipt).SelectedKeys));
        Assert.Equal(stored.FinalScore, new BaselineAScorer().Replay(stored.Snapshot).FinalScore);
        Assert.Contains("selected favorite recording", stored.GenerateExplanation());
        Assert.DoesNotContain(profile.Id.ToString(), JsonSerializer.Serialize(candidate));
    }
}
