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
        // Captured from the frozen 623f29a Application/Domain assemblies, not the changed implementation.
        Assert.Equal("6b8e6dd62d50a4ce6060a333ef7a2867879bd09f7077b29bd017fc596135faef", hash);
    }

    private static void SetId(LinerNotes.Domain.Common.BaseEntity entity, string key) =>
        typeof(LinerNotes.Domain.Common.BaseEntity).GetProperty(nameof(entity.Id))!.SetValue(entity,
            new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0, 16)));

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
