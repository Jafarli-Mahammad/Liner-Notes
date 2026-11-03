using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Discovery;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Taste;
using LinerNotes.Domain.Tests.Fixtures;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace LinerNotes.Application.Tests.Features.Discovery;

public sealed class DiscoveryOutputContractTests(ITestOutputHelper output)
{
    [Fact]
    public async Task TaglessRecordingConnectionKeepsReceiptAndReplaysWithoutInventingAffinityAcrossListeners()
    {
        var now = DateTimeOffset.Parse("2026-11-03T12:00:00Z");
        var configuration = new GenerationConfiguration { Origin = EvidenceOrigin.Synthetic };
        var resolver = Substitute.For<IRecommendationProviderResolver>();
        resolver.Resolve().Returns(new RecommendationProvider("fixture", Substitute.For<IRecommendationSource>(),
            Substitute.For<ICandidateHydrator>(), Substitute.For<ISeedTagSource>()));
        var response = new ResponseReference("ListenBrainz", "metadata.recording", "fixture-recording", EvidenceOrigin.Synthetic, new string('a', 64), now);
        var relation = response with { Method = "similar-recordings", RequestIdentity = Guid.NewGuid().ToString("D") };
        var missing = new CoverageGap("fixture", "metadata.recording", "recording_relationship_without_tags", response);
        var candidate = new HydratedCandidateTrack(Track.Create("Fixture recording", "Fixture credit"), WeightedTagVector.Empty,
            new("Fixture recording", "Fixture credit", null, [new(relation.RequestIdentity, ObservedValue<double>.Missing("not_supplied_by_endpoint"),
                relation, response, 1, null, null, ObservedValue<long>.Missing("not_supplied"))]), [], response, missing.Reason, [missing]) { TagScope = "none" };
        var preparation = Substitute.For<IDiscoveryPreparation>();
        preparation.GetAsync(Arg.Any<DiscoveryPoolRequest>(), true, Arg.Any<CancellationToken>()).Returns(
            new PreparedDiscoveryPool(new string('b', 64), "fixture", EvidenceOrigin.Synthetic, now, now.AddMinutes(5), CatalogText.Version,
                [candidate], [missing], 1000, new string('c', 64)));
        var store = Substitute.For<IDiscoveryBatchStore>();
        store.FindExcludedAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>()).Returns(new HashSet<string>());
        var runner = new PrepareDiscoveryBatch(new(resolver, configuration, new(), new FixedClock(now)), preparation, store, configuration, new());
        var first = ListenerProfile.Guest(now, TimeSpan.FromDays(7));
        var second = ListenerProfile.Guest(now, TimeSpan.FromDays(7));
        foreach (var (profile, tag) in new[] { (first, "electronic"), (second, "jazz"), (first, "electronic") })
        {
            var signal = TasteSignal.ForProfile(profile.Id, null, TasteTargetType.Tag, tag, 1, TasteSignalSource.InitialSeedManual, "Fixture");
            var result = await runner.PrepareAsync(new(profile, [signal], []), Guid.NewGuid(), true, default);
            var pick = Assert.Single(result.Picks);
            var stored = LinerNotes.Application.Common.Mappings.ScoreBreakdownJson.Deserialize(
                LinerNotes.Application.Common.Mappings.ScoreBreakdownJson.Serialize(pick.Breakdown));
            Assert.Equal(0, stored.TagSimilarityScore);
            Assert.Equal(0, stored.TagSimilarityRaw);
            Assert.Equal(stored.FinalScore, new BaselineAScorer().Replay(stored.Snapshot!).FinalScore);
            Assert.Equal("none", stored.Snapshot!.Evidence.TagScope);
            Assert.Equal(tag, Assert.Single(stored.Snapshot.TasteWeights).Key);
            Assert.Equal(relation.Store(), Assert.Single(stored.Snapshot.Evidence.DiscoveryPaths).SimilarityResponse);
            Assert.Contains(stored.Snapshot.Evidence.Gaps, gap => gap.Reason == missing.Reason);
            Assert.Contains("descriptive tags are missing", stored.GenerateExplanation());
            Assert.DoesNotContain(profile.Id.ToString(), JsonSerializer.Serialize(candidate));
        }
        Assert.True(candidate.TagVector.IsEmpty);
    }

    [Fact]
    public async Task EntirePreparedResultsMatchFrozenEightGenreContract()
    {
        var now = DateTimeOffset.Parse("2026-10-27T12:00:00Z");
        var configuration = new GenerationConfiguration { Origin = EvidenceOrigin.Synthetic };
        var resolver = Substitute.For<IRecommendationProviderResolver>();
        resolver.Resolve().Returns(new RecommendationProvider("fixture-contract", Substitute.For<IRecommendationSource>(),
            Substitute.For<ICandidateHydrator>(), Substitute.For<ISeedTagSource>()));
        var reference = new ResponseReference("fixture-contract", "metadata", "public-fixture", EvidenceOrigin.Synthetic,
            new string('a', 64), now);
        var candidates = BenchmarkSeedFixtures.AllCandidates.Select((candidate, index) =>
        {
            var track = Track.Create(candidate.Track.Title, candidate.Track.ArtistName, candidate.Track.AlbumTitle, candidate.Track.Mbid);
            SetId(track, "track:" + index);
            return new HydratedCandidateTrack(track, candidate.TagVector,
                new(track.Title, track.ArtistName, track.Mbid, [new("fixture", new(1, "1", null), reference, reference, index + 1,
                    null, null, new(null, null, "not_supplied"))]), [], reference, null,
                [new("fixture", "metadata", "fixture_missing_observation", reference)]) { TagScope = "recording" };
        }).ToArray();
        var preparation = Substitute.For<IDiscoveryPreparation>();
        var pool = new PreparedDiscoveryPool(new string('b', 64), "fixture-contract", EvidenceOrigin.Synthetic, now,
            now.AddMinutes(5), CatalogText.Version, candidates, [new("fixture", "pool", "fixture_gap", reference)], 1000, new string('c', 64));
        preparation.GetAsync(Arg.Any<DiscoveryPoolRequest>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(pool);
        var store = Substitute.For<IDiscoveryBatchStore>();
        var excluded = TrackIdentity.Aliases(candidates[1].Track).ToHashSet(StringComparer.Ordinal);
        store.FindExcludedAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>()).Returns(excluded);
        var runner = new PrepareDiscoveryBatch(new(resolver, configuration, new(), new FixedClock(now)), preparation, store, configuration, new());
        var results = new List<object>();
        // Paired fixtures span metal, hip-hop, electronic, jazz, pop, indie, classical and regional music.
        for (int genre = 0; genre < candidates.Length; genre += 2)
        {
            var profile = ListenerProfile.Guest(now, TimeSpan.FromDays(7)); SetId(profile, "profile");
            var signal = TasteSignal.ForProfile(profile.Id, null, TasteTargetType.Tag, candidates[genre].TagVector.Weights.Keys.First(),
                1, TasteSignalSource.InitialSeedManual, "Contract fixture."); SetId(signal, "signal:" + genre);
            foreach (bool acquisition in new[] { false, true })
            {
                var result = await runner.PrepareAsync(new(profile, [signal], []), Guid.Parse("11111111-1111-4111-8111-111111111111"), acquisition, default);
                results.Add(new { genre, acquisition, excluded = excluded.Order(StringComparer.Ordinal).ToArray(), result });
            }
        }
        string json = JsonSerializer.Serialize(results);
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        output.WriteLine("CONTRACT_JSON=" + json);
        output.WriteLine("CONTRACT_SHA256=" + hash);
        // Phase 3 adds feedback-policy metadata; normalized scoring and selection
        // match all 16 Phase 2 no-feedback genre/acquisition fixture outputs.
        Assert.Equal("fd538cedd9fe4344586e5f351bc76cabfab6fbe146b92ecc9f62392d81146c58", hash);
    }

    private static void SetId(LinerNotes.Domain.Common.BaseEntity entity, string key) =>
        typeof(LinerNotes.Domain.Common.BaseEntity).GetProperty(nameof(entity.Id))!.SetValue(entity,
            new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0, 16)));

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
