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

public sealed class FeedbackEvidenceTests
{
    [Fact]
    public async Task CompleteReceiptExplainsLearnedAttenuationAndReplaysWithoutRecomputingPrivateHistory()
    {
        var now = DateTimeOffset.UtcNow;
        var profile = ListenerProfile.Guest(now, TimeSpan.FromDays(7));
        var reference = new ResponseReference("fixture", "metadata", "fixture", EvidenceOrigin.Synthetic, new string('a', 64), now);
        var vector = WeightedTagVector.FromDictionary(new Dictionary<string, double> { ["fixture"] = 1 });
        var seeds = Substitute.For<ISeedTagSource>();
        seeds.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => new SeedTagSnapshot(
            call.Arg<string>(), vector, [], reference, null, []) { SharedUntilUtc = now.AddHours(1) });
        var resolver = Substitute.For<IRecommendationProviderResolver>();
        resolver.Resolve().Returns(new RecommendationProvider("fixture", Substitute.For<IRecommendationSource>(), Substitute.For<ICandidateHydrator>(), seeds));
        var track = Track.Create("Candidate", "Unheard artist");
        var candidate = new HydratedCandidateTrack(track, vector,
            new(track.Title, track.ArtistName, null, [new("fixture", new(1, "1", null), reference, reference, 1,
                null, null, new(null, null, "not_supplied"))]), [], reference, null, []) { TagScope = "recording" };
        var preparation = Substitute.For<IDiscoveryPreparation>();
        preparation.GetAsync(Arg.Any<DiscoveryPoolRequest>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(
            new PreparedDiscoveryPool(new string('b', 64), "fixture", EvidenceOrigin.Synthetic, now, now.AddMinutes(5),
                CatalogText.Version, [candidate], [], 100, new string('c', 64)));
        var store = Substitute.For<IDiscoveryBatchStore>();
        store.FindExcludedAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>()).Returns(new HashSet<string>());
        var config = new GenerationConfiguration { Origin = EvidenceOrigin.Synthetic };
        var history = Enumerable.Range(0, 4).Select(i => new FeedbackObservation(Guid.NewGuid(), profile.Id, null,
            "Learned artist", "Distinct " + i, null, i == 0 ? UserFeedback.Liked : UserFeedback.Disliked, null, now.UtcDateTime.AddMinutes(i - 10))).ToArray();
        var manual = TasteSignal.ForProfile(profile.Id, null, TasteTargetType.Tag, "fixture", 1, TasteSignalSource.InitialSeedManual, "Explicit tag");
        var seedPreparation = Substitute.For<IDiscoverySeedPreparation>();
        seedPreparation.GetAsync(Arg.Any<DiscoverySeedRequest>(), false, Arg.Any<CancellationToken>()).Returns(call => new SeedTagSnapshot(
            call.Arg<DiscoverySeedRequest>().Name, vector, [], reference, null, []) { SharedUntilUtc = now.AddHours(1) });
        var runner = new PrepareDiscoveryBatch(new(resolver, config, new(), TimeProvider.System, seedPreparation), preparation, store, config, new());
        var result = await runner.PrepareAsync(new(profile, [manual], [], FeedbackHistory: history), Guid.NewGuid(), false, default);
        var score = Assert.Single(result.Picks).Breakdown;
        var snapshot = score.Snapshot!;
        var support = Assert.Single(snapshot.Evidence.FeedbackSupport);
        Assert.Equal(3, support.NegativeRecordings);
        Assert.Equal(1, support.PositiveRecordings);
        Assert.True(support.Attenuated);
        Assert.Equal(.4, support.LearnedWeight);
        Assert.Equal(EffectiveTasteInputs.FeedbackPolicy, snapshot.Evidence.FeedbackPolicy);
        Assert.Equal(1, snapshot.Evidence.FeedbackSupportCount);
        Assert.Equal(64, snapshot.Evidence.FeedbackSupportSha256!.Length);
        var restored = LinerNotes.Application.Common.Mappings.ScoreBreakdownJson.Deserialize(
            LinerNotes.Application.Common.Mappings.ScoreBreakdownJson.Serialize(score));
        Assert.Equal(snapshot.Evidence.FeedbackSupport, restored.Snapshot!.Evidence.FeedbackSupport);
        Assert.Equal(score.FinalScore, new BaselineAScorer().Replay(restored.Snapshot).FinalScore, 12);
    }
}
