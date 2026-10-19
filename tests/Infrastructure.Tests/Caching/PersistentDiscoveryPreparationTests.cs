using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.Infrastructure.Caching;
using LinerNotes.Infrastructure.Discovery;
using LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LinerNotes.Infrastructure.Tests.Caching;

public sealed class PersistentDiscoveryPreparationTests
{
    [Theory]
    [InlineData(201, false)]
    [InlineData(256, false)]
    [InlineData(201, true)]
    [InlineData(256, true)]
    public async Task CanonicalSeedsAcceptCatalogDisplayNamesUpTo256Characters(int length, bool recording)
    {
        using var fixture = new Fixture();
        using var caches = new DiscoveryMemoryCaches(new());
        var seeds = fixture.Seeds(caches);
        var request = new DiscoverySeedRequest(Guid.NewGuid(), new string('x', length), !recording, recording);
        await seeds.GetAsync(request, true, default);
        await Assert.ThrowsAsync<ArgumentException>(() => seeds.GetAsync(request with { Name = new string('x', 257) }, true, default));
    }

    [Fact]
    public async Task PreparedLaneMissDoesNotAcquireOrWrite()
    {
        using var fixture = new Fixture();
        using var caches = new DiscoveryMemoryCaches(new());
        using var memory = fixture.Memory(caches);
        var pools = new PersistentDiscoveryPreparation(memory, fixture.Evidence);
        var seeds = fixture.Seeds(caches);
        await Assert.ThrowsAsync<DiscoveryPreparationRequiredException>(() => pools.GetAsync(new(Guid.NewGuid(), "artist", false), false, default));
        await Assert.ThrowsAsync<DiscoveryPreparationRequiredException>(() => seeds.GetAsync(new(Guid.NewGuid(), "album", true), false, default));
        Assert.Equal(0, fixture.Acquisitions);
        Assert.Equal(0, fixture.Coordinator.Writes);
    }

    [Fact]
    public async Task PublicPoolsAndSeedsSurviveMemoryRestartWithoutProviderWork()
    {
        using var fixture = new Fixture();
        var poolRequest = new DiscoveryPoolRequest(Guid.NewGuid(), "artist", false);
        var seedRequest = new DiscoverySeedRequest(Guid.NewGuid(), "album", true);
        PreparedDiscoveryPool acquired;
        SeedTagSnapshot seed;
        using (var caches = new DiscoveryMemoryCaches(new()))
        using (var memory = fixture.Memory(caches))
        {
            acquired = await new PersistentDiscoveryPreparation(memory, fixture.Evidence).GetAsync(poolRequest, true, default);
            seed = await fixture.Seeds(caches).GetAsync(seedRequest, true, default);
        }
        Assert.NotEmpty(acquired.Candidates);
        Assert.Equal(2, fixture.Acquisitions);
        Assert.Equal(2, fixture.Store.Documents.Count);
        using var restartedCaches = new DiscoveryMemoryCaches(new());
        using var restartedMemory = fixture.Memory(restartedCaches);
        var restored = await new PersistentDiscoveryPreparation(restartedMemory, fixture.Evidence).GetAsync(poolRequest, false, default);
        var restoredSeed = await fixture.Seeds(restartedCaches).GetAsync(seedRequest, false, default);
        Assert.Equal(acquired.ContentSha256, restored.ContentSha256);
        Assert.Equal(seed.Vector, restoredSeed.Vector);
        Assert.Equal(2, fixture.Acquisitions);
        Assert.DoesNotContain(acquired.Candidates[0].Track.Id.ToString(), fixture.Store.Documents[acquired.Key].PayloadJson);
    }

    [Fact]
    public async Task ExpiredEvidenceCannotServePreparedLane()
    {
        using var fixture = new Fixture();
        using var caches = new DiscoveryMemoryCaches(new());
        using var memory = fixture.Memory(caches);
        var pools = new PersistentDiscoveryPreparation(memory, fixture.Evidence);
        var request = new DiscoveryPoolRequest(Guid.NewGuid(), "artist", false);
        await pools.GetAsync(request, true, default);
        fixture.Clock.Now += TimeSpan.FromMinutes(11);
        await Assert.ThrowsAsync<DiscoveryPreparationRequiredException>(() => pools.GetAsync(request, false, default));
        Assert.Equal(1, fixture.Acquisitions);
    }

    [Fact]
    public async Task NonRetainableEvidenceIsReturnedOnlyToCurrentAcquisition()
    {
        using var fixture = new Fixture();
        var now = fixture.Clock.Now;
        const string key = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var document = PublicEvidenceCodec.Document(key, PublicEvidenceKind.Lookup, "listenbrainz", EvidenceOrigin.Synthetic,
            "lookup-test", new { Items = Array.Empty<string>() }, now, now);
        Assert.Equal(document, await fixture.Evidence.GetAsync(key, document.Kind, document.Version, true, _ => Task.FromResult(document), default));
        Assert.Empty(fixture.Store.Documents);
        Assert.Empty(fixture.Store.Leases);
        await Assert.ThrowsAsync<DiscoveryPreparationRequiredException>(() => fixture.Evidence.GetAsync(key, document.Kind,
            document.Version, false, _ => throw new InvalidOperationException(), default));
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Coordinator : IDiscoveryWriteCoordinator
    {
        public Task<T> ReclaimAsync<T>(Func<CancellationToken, Task<T>> delete, CancellationToken ct) => ExecuteAsync(0, delete, ct);
        public bool Active;
        public int Writes;
        public async Task<T> ExecuteAsync<T>(long bytes, Func<CancellationToken, Task<T>> write, CancellationToken ct)
        {
            Assert.False(Active);
            Active = true; Writes++;
            try { return await write(ct); }
            finally { Active = false; }
        }
    }
    private sealed class Store : IPublicEvidenceStore
    {
        public readonly Dictionary<string, PublicEvidenceDocument> Documents = [];
        public readonly Dictionary<string, PublicEvidenceLease> Leases = [];
        public Task<PublicEvidenceDocument?> FindAsync(string key, DateTimeOffset now, CancellationToken ct)
            => Task.FromResult(Documents.TryGetValue(key, out var value) && value.ExpiresAtUtc > now ? value : null);
        public Task<PublicEvidenceLease?> ClaimAsync(string key, PublicEvidenceKind kind, DateTimeOffset now, TimeSpan duration, CancellationToken ct)
        {
            var lease = new PublicEvidenceLease(key, Guid.NewGuid(), now + duration);
            Leases.Add(key, lease);
            return Task.FromResult<PublicEvidenceLease?>(lease);
        }
        public Task PublishAsync(PublicEvidenceLease lease, PublicEvidenceDocument document, DateTimeOffset now, long bytes, int entries, CancellationToken ct)
        {
            Assert.Equal(lease, Leases[lease.Key]);
            Documents[lease.Key] = document; Leases.Remove(lease.Key);
            return Task.CompletedTask;
        }
        public Task ReleaseAsync(PublicEvidenceLease lease, DateTimeOffset now, CancellationToken ct)
        { Leases.Remove(lease.Key); return Task.CompletedTask; }
        public Task<int> DeleteExpiredAsync(DateTimeOffset now, int limit, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class Fixture : IDisposable, IRecommendationProviderResolver
    {
        public readonly Clock Clock = new();
        public readonly Coordinator Coordinator = new();
        public readonly Store Store = new();
        public int Acquisitions;
        private readonly ServiceProvider services;
        private readonly GenerationConfiguration generation = new() { Origin = EvidenceOrigin.Synthetic };
        public SharedPublicEvidence Evidence { get; }
        public Fixture()
        {
            services = new ServiceCollection().AddSingleton<IPublicEvidenceStore>(Store)
                .AddSingleton<IDiscoveryWriteCoordinator>(Coordinator).AddSingleton<IRecommendationProviderResolver>(this).BuildServiceProvider();
            Evidence = new(services.GetRequiredService<IServiceScopeFactory>(), generation, new DiscoveryRuntimeOptions(), Clock);
        }
        public SharedDiscoveryPreparation Memory(DiscoveryMemoryCaches caches)
            => new(services.GetRequiredService<IServiceScopeFactory>(), caches, generation, Clock);
        public SharedSeedPreparation Seeds(DiscoveryMemoryCaches caches)
            => new(services.GetRequiredService<IServiceScopeFactory>(), Evidence, caches, generation, Clock);
        public RecommendationProvider Resolve()
        {
            Assert.False(Coordinator.Active);
            Acquisitions++;
            var provider = new ListenBrainzRecommendationProvider(new FixtureListenBrainzApiClient());
            return new("listenbrainz", provider, provider, provider);
        }
        public void Dispose() => services.Dispose();
    }
}
