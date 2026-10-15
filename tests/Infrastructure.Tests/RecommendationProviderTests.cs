using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Infrastructure.RecommendationSources.LastFm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LinerNotes.Infrastructure.Tests;

public sealed class RecommendationProviderTests
{
    [Fact]
    public void LastFmBundleIsCoherentAndStableWithinScope()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Generation:Provider"] = "lastfm" }).Build();
        using var services = new ServiceCollection().AddLogging().AddInfrastructure(config).BuildServiceProvider();
        using var scope = services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IRecommendationProviderResolver>();
        var bundle = resolver.Resolve();
        Assert.Equal("lastfm", bundle.Id);
        Assert.IsType<LastFmRecommendationSource>(bundle.Source);
        Assert.IsType<LastFmCandidateHydrator>(bundle.Hydrator);
        Assert.IsType<BatchTagCachingLastFmClient>(bundle.SeedTags);
        Assert.Same(bundle, resolver.Resolve());
        Assert.Same(bundle.Source, scope.ServiceProvider.GetRequiredService<IRecommendationSource>());
        Assert.Same(bundle.Hydrator, scope.ServiceProvider.GetRequiredService<ICandidateHydrator>());
        Assert.Same(bundle.SeedTags, scope.ServiceProvider.GetRequiredService<ISeedTagSource>());
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("")]
    [InlineData("LASTFM")]
    public void InvalidProviderFailsClosed(string provider)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Generation:Provider"] = provider }).Build();
        using var services = new ServiceCollection().AddLogging().AddInfrastructure(config).BuildServiceProvider();
        Assert.Throws<ArgumentException>(() => services.GetRequiredService<GenerationConfiguration>().Scoring());
        using var scope = services.CreateScope();
        Assert.Throws<GenerationStoppedException>(() => scope.ServiceProvider.GetRequiredService<IRecommendationProviderResolver>().Resolve());
    }
}
