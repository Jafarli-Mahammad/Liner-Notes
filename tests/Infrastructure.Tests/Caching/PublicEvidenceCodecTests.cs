using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Infrastructure.Caching;
using LinerNotes.Infrastructure.Discovery;
using LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace LinerNotes.Infrastructure.Tests.Caching;
public sealed class PublicEvidenceCodecTests
{
    [Fact]
    public async Task PersistedPoolRoundTripPreservesFactsAndExcludesTransientTrackIds()
    {
        using var services = new ServiceCollection().AddScoped<IRecommendationProviderResolver>(_ => new Resolver()).BuildServiceProvider();
        using var caches = new DiscoveryMemoryCaches(new());
        using var preparation = new SharedDiscoveryPreparation(services.GetRequiredService<IServiceScopeFactory>(), caches,
            new() { Origin = EvidenceOrigin.Synthetic }, TimeProvider.System);
        var pool = await preparation.GetAsync(new(Guid.NewGuid(), "Synthetic artist", false), default);
        var document = PublicEvidenceCodec.Encode(pool);
        var restored = PublicEvidenceCodec.DecodePool(document);
        Assert.Equal(pool.ContentSha256, restored.ContentSha256);
        Assert.Equal(pool.Candidates.Select(c => c.Track.TrackKey), restored.Candidates.Select(c => c.Track.TrackKey));
        Assert.Equal(pool.Candidates[0].TagVector, restored.Candidates[0].TagVector);
        Assert.Equal(pool.Candidates[0].Tags.ToArray(), restored.Candidates[0].Tags.ToArray());
        Assert.DoesNotContain(pool.Candidates[0].Track.Id.ToString(), document.PayloadJson);
        Assert.DoesNotContain("ProfileId", document.PayloadJson);
        var bad = document with { Origin = EvidenceOrigin.Live };
        await Assert.ThrowsAsync<GenerationStoppedException>(() => Task.FromResult(PublicEvidenceCodec.DecodePool(bad)));
    }
    [Fact]
    public async Task SeedRoundTripRetainsCanonicalResponseAndNormalizationEvidence()
    {
        var source = new ListenBrainzRecommendationProvider(new FixtureListenBrainzApiClient());
        var seed = await source.GetAlbumAsync(Guid.NewGuid(), "Synthetic album", default);
        var now = DateTimeOffset.UtcNow;
        var document = PublicEvidenceCodec.EncodeSeed(new string('a', 64), "listenbrainz", seed, now, now.AddMinutes(10));
        var restored = PublicEvidenceCodec.DecodeSeed(document);
        Assert.Equal(seed.Vector, restored.Vector);
        Assert.Equal(seed.Response, restored.Response);
        Assert.Equal(seed.Tags, restored.Tags);
    }
    private sealed class Resolver : IRecommendationProviderResolver
    {
        public RecommendationProvider Resolve()
        {
            var provider = new ListenBrainzRecommendationProvider(new FixtureListenBrainzApiClient());
            return new("listenbrainz", provider, provider, provider);
        }
    }
}
