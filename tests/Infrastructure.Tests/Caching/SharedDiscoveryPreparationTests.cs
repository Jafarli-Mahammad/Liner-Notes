using System.Collections.Concurrent;
using System.Text;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Infrastructure.Caching;
using LinerNotes.Infrastructure.Discovery;
using LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace LinerNotes.Infrastructure.Tests.Caching;
public sealed class SharedDiscoveryPreparationTests
{
    [Theory]
    [InlineData(201, false)]
    [InlineData(256, false)]
    [InlineData(201, true)]
    [InlineData(256, true)]
    public async Task CanonicalPoolsAcceptCatalogDisplayNamesUpTo256Characters(int length, bool recording)
    {
        var clock = new Clock();
        var calls = new ConcurrentBag<ListenBrainzRequestKind>();
        using var services = Services(calls, clock, live: false, cacheable: true);
        using var caches = new DiscoveryMemoryCaches(new());
        using var preparation = new SharedDiscoveryPreparation(services.GetRequiredService<IServiceScopeFactory>(), caches,
            new() { Origin = EvidenceOrigin.Synthetic }, clock);
        var request = new DiscoveryPoolRequest(Guid.NewGuid(), new string('x', length), false, recording);
        Assert.NotEmpty((await preparation.GetAsync(request, default)).Candidates);
        Assert.Throws<ArgumentException>(() => preparation.KeyFor(request with { Seed = new string('x', 257) }));
    }

    [Fact]
    public async Task IdenticalPublicWorkIsCoalescedAndCanonicalArtistBypassesNameSearch()
    {
        var clock = new Clock();
        var calls = new ConcurrentBag<ListenBrainzRequestKind>();
        using var services = Services(calls, clock, live: false, cacheable: true);
        using var caches = new DiscoveryMemoryCaches(new());
        using var preparation = new SharedDiscoveryPreparation(services.GetRequiredService<IServiceScopeFactory>(), caches,
            new() { Origin = EvidenceOrigin.Synthetic }, clock);
        var request = new DiscoveryPoolRequest(Guid.NewGuid(), "Same name", false);
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => preparation.GetAsync(request, default)));
        Assert.NotEmpty(results[0].Candidates);
        Assert.All(results, r => Assert.Same(results[0], r));
        Assert.DoesNotContain(ListenBrainzRequestKind.ArtistSearch, calls);
        Assert.Equal(1, calls.Count(k => k == ListenBrainzRequestKind.ArtistRadio));
        Assert.Equal(64, results[0].ContentSha256.Length);
        Assert.True(results[0].SerializedBytes > 0);
        await preparation.GetAsync(request with { ArtistId = Guid.NewGuid() }, default);
        Assert.Equal(2, calls.Count(k => k == ListenBrainzRequestKind.ArtistRadio));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedReuseHonorsActualSourceCacheDeadline(bool cacheable)
    {
        var clock = new Clock();
        var calls = new ConcurrentBag<ListenBrainzRequestKind>();
        using var services = Services(calls, clock, live: true, cacheable);
        using var caches = new DiscoveryMemoryCaches(new());
        using var preparation = new SharedDiscoveryPreparation(services.GetRequiredService<IServiceScopeFactory>(), caches,
            new() { Origin = EvidenceOrigin.Live, LiveEnabled = true }, clock);
        var request = new DiscoveryPoolRequest(Guid.NewGuid(), "Public seed", false);
        var first = await preparation.GetAsync(request, default);
        await preparation.GetAsync(request, default);
        Assert.Equal(cacheable ? 1 : 2, calls.Count(k => k == ListenBrainzRequestKind.ArtistRadio));
        if (cacheable)
        {
            Assert.Equal(clock.GetUtcNow().AddSeconds(10), first.ExpiresAtUtc);
            clock.Advance(TimeSpan.FromSeconds(11));
            var refreshed = await preparation.GetAsync(request, default);
            Assert.Equal(first.ContentSha256, refreshed.ContentSha256);
            Assert.Equal(2, calls.Count(k => k == ListenBrainzRequestKind.ArtistRadio));
        }
        else Assert.Null(first.ExpiresAtUtc);
    }
    [Fact]
    public async Task EvaluationMaterialCannotEnterProductPreparation()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        using var caches = new DiscoveryMemoryCaches(new());
        using var preparation = new SharedDiscoveryPreparation(services.GetRequiredService<IServiceScopeFactory>(), caches,
            new() { Origin = EvidenceOrigin.Recorded }, TimeProvider.System);
        var error = await Assert.ThrowsAsync<GenerationStoppedException>(() => preparation.GetAsync(new(null, "jazz", true), default));
        Assert.Equal("evaluation_evidence_ineligible", error.Message);
    }
    [Fact]
    public async Task AlbumParserUsesReleaseGroupTagsAndIgnoresArtistTags()
    {
        Guid id = Guid.NewGuid();
        var request = new ListenBrainzRequest(ListenBrainzRequestKind.AlbumMetadata, id.ToString("D"));
        Assert.Equal($"1/metadata/release_group/?release_group_mbids={id:D}&inc=tag", request.RelativeUri());
        var bytes = Encoding.UTF8.GetBytes("{\"" + id.ToString("D") + "\":{\"tag\":{\"artist\":[{\"tag\":\"wrong\",\"count\":999}],\"release_group\":[{\"tag\":\"ambient\",\"count\":5},{\"tag\":\"jazz\",\"count\":-1}]}}}");
        var response = ListenBrainzResponse.Parse(request, bytes, EvidenceOrigin.Synthetic, DateTimeOffset.UnixEpoch);
        var tags = ListenBrainzRecommendationProvider.ParseAlbumTags(response, id, "Album");
        Assert.Equal(1, tags.Vector["ambient"]);
        Assert.Equal(0, tags.Vector["wrong"]);
        Assert.Equal(0, tags.Vector["jazz"]);
        Assert.All(tags.Tags, t => Assert.Equal("release-group:" + id.ToString("D"), t.EntityIdentity));
        var provider = new ListenBrainzRecommendationProvider(new FixtureListenBrainzApiClient());
        Assert.Equal(1, (await provider.GetAlbumAsync(id, "Album", default)).Vector["electronic"]);
    }
    private static ServiceProvider Services(ConcurrentBag<ListenBrainzRequestKind> calls, Clock clock, bool live, bool cacheable) =>
        new ServiceCollection().AddScoped<IRecommendationProviderResolver>(_ => new Resolver(new ListenBrainzRecommendationProvider(new Client(calls, clock, live, cacheable)))).BuildServiceProvider();
    private sealed class Resolver(ListenBrainzRecommendationProvider provider) : IRecommendationProviderResolver
    { public RecommendationProvider Resolve() => new("listenbrainz", provider, provider, provider); }
    private sealed class Client(ConcurrentBag<ListenBrainzRequestKind> calls, Clock clock, bool live, bool cacheable) : IListenBrainzApiClient
    {
        private readonly FixtureListenBrainzApiClient fixture = new();
        public async Task<ListenBrainzResponse> SendAsync(ListenBrainzRequest request, CancellationToken ct = default)
        {
            calls.Add(request.Kind);
            var result = await fixture.SendAsync(request, ct);
            return result with { Reference = result.Reference with { Origin = live ? EvidenceOrigin.Live : EvidenceOrigin.Synthetic },
                SharedCacheUntilUtc = cacheable ? clock.GetUtcNow().AddSeconds(10) : null };
        }
    }
    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 10, 16, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan time) => now += time;
    }
}
