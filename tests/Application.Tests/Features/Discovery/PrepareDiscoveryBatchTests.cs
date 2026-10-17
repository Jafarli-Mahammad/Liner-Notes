using System.Text.Json;
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

public sealed class PrepareDiscoveryBatchTests
{
    [Fact]
    public async Task MergesPublicPathsSuppressesHistoryAndPersistsReplayableBatchEvidence()
    {
        var configuration = new GenerationConfiguration { Origin = EvidenceOrigin.Synthetic };
        var resolver = Substitute.For<IRecommendationProviderResolver>();
        resolver.Resolve().Returns(new RecommendationProvider("listenbrainz", Substitute.For<IRecommendationSource>(),
            Substitute.For<ICandidateHydrator>(), Substitute.For<ISeedTagSource>()));
        var materializer = new MaterializeDiscoveryTaste(resolver, configuration, new(), TimeProvider.System);
        var preparation = Substitute.For<IDiscoveryPreparation>();
        var store = Substitute.For<IDiscoveryBatchStore>();
        var profile = ListenerProfile.Guest(DateTimeOffset.UtcNow, TimeSpan.FromDays(7));
        var now = DateTimeOffset.UtcNow;
        var first = Candidate("First", "Artist", "jazz");
        var duplicate = Candidate("First", "Artist", "ambient");
        var known = Candidate("Known", "Known artist", "jazz");
        foreach (string style in new[] { "jazz", "ambient" })
            preparation.GetAsync(new(null, style, true), false, Arg.Any<CancellationToken>()).Returns(
                new PreparedDiscoveryPool(new string('a', 64), "listenbrainz", EvidenceOrigin.Synthetic, now, now.AddMinutes(5),
                    CatalogText.Version, style == "jazz" ? [first, known] : [duplicate], [], 1000, new string('b', 64)));
        store.FindExcludedAsync(profile.Id, Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(TrackIdentity.Aliases(known.Track).ToHashSet(StringComparer.Ordinal));
        var signals = new[] { "jazz", "ambient" }.Select(t => TasteSignal.ForProfile(profile.Id, null,
            TasteTargetType.Tag, t, 1, TasteSignalSource.InitialSeedManual, "Selected style.")).ToArray();
        Guid batchId = Guid.NewGuid();
        var result = await new PrepareDiscoveryBatch(materializer, preparation, store, configuration, new())
            .PrepareAsync(new(profile, signals, []), batchId, false, default);
        var pick = Assert.Single(result.Picks);
        Assert.Equal("First", pick.Track.Title);
        Assert.Equal(batchId, pick.Breakdown.Snapshot!.DiscoveryBatchId);
        Assert.Equal(2, pick.Breakdown.Snapshot.Evidence.DiscoveryPaths.Count);
        Assert.Equal(pick.Breakdown.FinalScore, new BaselineAScorer().Replay(pick.Breakdown.Snapshot).FinalScore);
        Assert.Equal(now.AddMinutes(5), result.EvidenceExpiry);
        using var coverage = JsonDocument.Parse(result.CoverageJson);
        Assert.Equal(1, coverage.RootElement.GetProperty("SuppressedCandidates").GetInt32());
        Assert.True(result.ReservationBytes > 65_536);
        await preparation.DidNotReceiveWithAnyArgs().GetAsync(default!, default);
    }

    private static HydratedCandidateTrack Candidate(string title, string artist, string seed)
    {
        var response = new ResponseReference("listenbrainz", "fixture", seed, EvidenceOrigin.Synthetic,
            new string('a', 64), DateTimeOffset.UnixEpoch);
        var path = new DiscoveryPath(seed, new(1, "1", null), null, response, 1, null, null, new(null, null, "not_supplied"));
        return new(new Track(title, artist), WeightedTagVector.FromDictionary(new Dictionary<string, double> { ["jazz"] = 1 }),
            new(title, artist, null, [path]), [], response, null);
    }
}
