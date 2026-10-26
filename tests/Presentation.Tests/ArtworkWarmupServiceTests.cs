using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.Presentation.Background;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace LinerNotes.Presentation.Tests;

public sealed class ArtworkWarmupServiceTests
{
    [Fact]
    public async Task WarmsPublicCatalogIdentitiesAndSkipsSyntheticOnItsOwnLane()
    {
        var publicId = Guid.NewGuid();
        var syntheticId = Guid.NewGuid();
        var recent = Substitute.For<IRecentRecommendations>();
        recent.ReadAsync(null, 50, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new RecentMusicPage([
                new(2, publicId, "Public track", "Artist", "Public", "MusicBrainz"),
                new(1, syntheticId, "Fixture track", "Artist", "Synthetic", "Fixture")], null));
        var artwork = Substitute.For<IRecordingArtwork>();
        var warmed = new TaskCompletionSource<IReadOnlyList<Guid>>(TaskCreationOptions.RunContinuationsAsynchronously);
        artwork.WarmAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            warmed.TrySetResult(call.Arg<IReadOnlyList<Guid>>());
            return Task.CompletedTask;
        });
        using var services = new ServiceCollection().AddSingleton(recent).AddSingleton(artwork).BuildServiceProvider();
        using var worker = new ArtworkWarmupService(services.GetRequiredService<IServiceScopeFactory>(),
            new DiscoveryRuntimeOptions { RunJobs = true }, TimeProvider.System, NullLogger<ArtworkWarmupService>.Instance);
        await worker.StartAsync(default);
        Assert.Equal(new[] { publicId }, await warmed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        await worker.StopAsync(default);
    }
}
