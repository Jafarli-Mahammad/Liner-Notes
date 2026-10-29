using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.Infrastructure.Artwork;
using LinerNotes.Presentation.Background;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace LinerNotes.Presentation.Tests;

public sealed class ArtworkWarmupServiceTests
{
    private static ArtworkAcquisition Plan(Guid id) => new(id, id, null, null, DateTimeOffset.UtcNow.AddMinutes(5));
    private static IRecentRecommendations EmptyRecent()
    {
        var recent = Substitute.For<IRecentRecommendations>();
        recent.ReadAsync(Arg.Any<long?>(), 50, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(new RecentMusicPage([], null));
        return recent;
    }
    private static ArtworkWarmupService Worker(ServiceProvider services, IArtworkQueue queue) => new(
        services.GetRequiredService<IServiceScopeFactory>(), new() { RunJobs = true }, TimeProvider.System,
        NullLogger<ArtworkWarmupService>.Instance, queue);

    [Fact]
    public async Task ArbitraryRequestedIdsNeverReachTheProvider()
    {
        var id = Guid.NewGuid();
        using var cache = new RecordingArtworkCache();
        var queue = new ObservedQueue(cache, id);
        cache.Enqueue([id], priority: true);
        var catalog = Substitute.For<IArtworkCatalog>();
        catalog.EligibleAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Guid>>([]));
        var artwork = Substitute.For<IRecordingArtwork>();
        artwork.PrepareAcquisitionAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            Assert.Empty(call.Arg<IReadOnlyList<Guid>>());
            return Task.FromResult<IReadOnlyList<ArtworkAcquisition>>([]);
        });
        using var services = new ServiceCollection().AddSingleton(EmptyRecent()).AddSingleton(catalog).AddSingleton(artwork).BuildServiceProvider();
        using var worker = Worker(services, queue);
        await worker.StartAsync(default);
        try
        {
            await queue.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await artwork.DidNotReceive().AcquireAsync(Arg.Any<ArtworkAcquisition>(), Arg.Any<CancellationToken>());
        }
        finally { await worker.StopAsync(default); }
    }

    [Fact]
    public async Task WarmsPublicCatalogIdentitiesAndSkipsSyntheticOnItsOwnLane()
    {
        var publicId = Guid.NewGuid(); var syntheticId = Guid.NewGuid();
        var recent = Substitute.For<IRecentRecommendations>();
        recent.ReadAsync(null, 50, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(new RecentMusicPage([
            new(2, publicId, "Public track", "Artist", "Public", "MusicBrainz"),
            new(1, syntheticId, "Fixture track", "Artist", "Synthetic", "Fixture")], null));
        var artwork = Substitute.For<IRecordingArtwork>();
        artwork.PrepareAcquisitionAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            Assert.Equal(new[] { publicId }, call.Arg<IReadOnlyList<Guid>>());
            return Task.FromResult<IReadOnlyList<ArtworkAcquisition>>([Plan(publicId)]);
        });
        using var cache = new RecordingArtworkCache(); var queue = new ObservedQueue(cache, publicId);
        var catalog = Substitute.For<IArtworkCatalog>();
        catalog.EligibleAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.Arg<IReadOnlyList<Guid>>()));
        using var services = new ServiceCollection().AddSingleton(recent).AddSingleton(artwork).AddSingleton(catalog).BuildServiceProvider();
        using var worker = Worker(services, queue);
        await worker.StartAsync(default);
        try
        {
            await queue.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await artwork.Received(1).AcquireAsync(Arg.Is<ArtworkAcquisition>(plan => plan.RecordingId == publicId), Arg.Any<CancellationToken>());
        }
        finally { await worker.StopAsync(default); }
    }

    [Fact]
    public async Task VisibleDemandUsesTheNextFreeImageLaneEvenWithTwoPreparedMaintenanceBatches()
    {
        using var cache = new RecordingArtworkCache();
        var ids = Enumerable.Range(0, 16).Select(_ => Guid.NewGuid()).ToArray();
        var visible = Guid.NewGuid(); var queue = new ObservedQueue(cache, visible);
        cache.Enqueue(ids);
        var slowStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fastStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var demanded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSlow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFast = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var catalog = Substitute.For<IArtworkCatalog>();
        catalog.EligibleAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.Arg<IReadOnlyList<Guid>>()));
        var artwork = Substitute.For<IRecordingArtwork>();
        artwork.PrepareAcquisitionAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<IReadOnlyList<ArtworkAcquisition>>(call.Arg<IReadOnlyList<Guid>>().Select(Plan).ToArray()));
        artwork.AcquireAsync(Arg.Any<ArtworkAcquisition>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var id = call.Arg<ArtworkAcquisition>().RecordingId;
            if (id == ids[0] || id == ids[8]) { slowStarted.TrySetResult(); await releaseSlow.Task.WaitAsync(call.Arg<CancellationToken>()); }
            else if (id == visible) demanded.TrySetResult();
            else { fastStarted.TrySetResult(); await releaseFast.Task.WaitAsync(call.Arg<CancellationToken>()); }
        });
        using var services = new ServiceCollection().AddSingleton(EmptyRecent()).AddSingleton(catalog).AddSingleton(artwork).BuildServiceProvider();
        using var worker = Worker(services, queue);
        await worker.StartAsync(default);
        try
        {
            await Task.WhenAll(slowStarted.Task, fastStarted.Task).WaitAsync(TimeSpan.FromSeconds(5));
            cache.Enqueue([visible], priority: true);
            await queue.Prepared.Task.WaitAsync(TimeSpan.FromSeconds(5));
            releaseFast.TrySetResult();
            await demanded.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(releaseSlow.Task.IsCompleted);
        }
        finally { releaseSlow.TrySetResult(); releaseFast.TrySetResult(); await worker.StopAsync(default); }
    }

    [Fact]
    public async Task ExpiredPublicEligibilityBetweenPreparationAndAcquisitionPreventsDownload()
    {
        var id = Guid.NewGuid(); using var cache = new RecordingArtworkCache(); var queue = new ObservedQueue(cache, id);
        cache.Enqueue([id]);
        var catalog = Substitute.For<IArtworkCatalog>(); int checks = 0;
        catalog.EligibleAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<Guid>>(Interlocked.Increment(ref checks) == 1 ? [id] : []));
        var artwork = Substitute.For<IRecordingArtwork>();
        artwork.PrepareAcquisitionAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ArtworkAcquisition>>([Plan(id)]));
        using var services = new ServiceCollection().AddSingleton(EmptyRecent()).AddSingleton(catalog).AddSingleton(artwork).BuildServiceProvider();
        using var worker = Worker(services, queue);
        await worker.StartAsync(default);
        try
        {
            await queue.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(2, checks);
            await artwork.DidNotReceive().AcquireAsync(Arg.Any<ArtworkAcquisition>(), Arg.Any<CancellationToken>());
        }
        finally { await worker.StopAsync(default); }
    }

    private sealed class ObservedQueue(IArtworkQueue inner, Guid observed) : IArtworkQueue
    {
        public TaskCompletionSource Prepared { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Enqueue(IReadOnlyList<Guid> ids, bool priority = false) => inner.Enqueue(ids, priority);
        public Task<IReadOnlyList<Guid>> TakeAsync(CancellationToken ct) => inner.TakeAsync(ct);
        public Task<ArtworkAcquisition> TakePreparedAsync(CancellationToken ct) => inner.TakePreparedAsync(ct);
        public void QueuePrepared(IReadOnlyList<ArtworkAcquisition> plans)
        { inner.QueuePrepared(plans); if (plans.Any(plan => plan.RecordingId == observed)) Prepared.TrySetResult(); }
        public void Complete(IReadOnlyList<Guid> ids, bool retryable = false)
        { inner.Complete(ids, retryable); if (ids.Contains(observed)) Completed.TrySetResult(); }
    }
}
