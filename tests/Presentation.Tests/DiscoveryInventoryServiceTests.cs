using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.Domain.Scoring;
using LinerNotes.Presentation.Background;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace LinerNotes.Presentation.Tests;

public sealed class DiscoveryInventoryServiceTests
{
    [Fact]
    public async Task RecordedEvaluationCannotStartGuestRuntime()
    {
        using var services = new ServiceCollection().AddSingleton(new DiscoveryRuntimeOptions { RunJobs = true })
            .AddSingleton(new GenerationConfiguration { Origin = EvidenceOrigin.Recorded }).BuildServiceProvider();
        using var runner = new DiscoveryInventoryService(services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System,
            NullLogger<DiscoveryInventoryService>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.StartAsync(default));
    }

    [Fact]
    public async Task UnknownInventoryDoesNotCrashReadOnlyHostOrReconcileItself()
    {
        var attempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inventory = Substitute.For<IDigestGenerationStore>();
        inventory.AcquireBatchAsync(Arg.Any<CancellationToken>()).Returns(new Lease());
        inventory.ReadStorageAsync(Arg.Any<CancellationToken>()).Returns(new GenerationStorageState(1024, "managed-v1:1:0:fixture"));
        var storage = Substitute.For<ICoordinatedGenerationStorage>();
        storage.RefreshKnownInventoryAsync(Arg.Any<GenerationStorageState>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            attempted.TrySetResult();
            return Task.FromException(new GenerationStoppedException("unknown_storage_inventory"));
        });
        var resolver = Substitute.For<IRecommendationProviderResolver>();
        resolver.Resolve().Returns(new RecommendationProvider("listenbrainz", Substitute.For<IRecommendationSource>(),
            Substitute.For<ICandidateHydrator>(), Substitute.For<ISeedTagSource>()));
        using var services = new ServiceCollection().AddSingleton(new DiscoveryRuntimeOptions { RunJobs = true })
            .AddSingleton(new GenerationConfiguration { Origin = EvidenceOrigin.Synthetic })
            .AddSingleton(inventory).AddSingleton(storage).AddSingleton(resolver).AddScoped<MaintainDiscoveryStorage>().BuildServiceProvider();
        using var runner = new DiscoveryInventoryService(services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System,
            NullLogger<DiscoveryInventoryService>.Instance);

        await runner.StartAsync(default);
        await attempted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(runner.ExecuteTask!.IsCompleted);
        await runner.StopAsync(default);
    }

    private sealed class Lease : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
