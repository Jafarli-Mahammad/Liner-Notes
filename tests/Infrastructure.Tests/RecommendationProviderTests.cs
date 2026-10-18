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
    public void RegisteredAdapterCanBeSelectedWithoutChangingResolverOrApplication()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Generation:Provider"] = "alternate", ["Generation:Origin"] = "Synthetic",
            ["Generation:IngestionPolicyVersion"] = "alternate-tags-v2"
        }).Build();
        var adapter = new RecommendationProvider("alternate",
            NSubstitute.Substitute.For<IRecommendationSource>(),
            NSubstitute.Substitute.For<ICandidateHydrator>(),
            NSubstitute.Substitute.For<ISeedTagSource>());
        using var services = new ServiceCollection().AddLogging().AddInfrastructure(config)
            .AddKeyedScoped<RecommendationProvider>("alternate", (_, _) => adapter).BuildServiceProvider();
        using var scope = services.CreateScope();

        services.GetRequiredService<GenerationConfiguration>().Scoring();
        Assert.Same(adapter, scope.ServiceProvider.GetRequiredService<IRecommendationProviderResolver>().Resolve());
        Assert.Same(adapter.Source, scope.ServiceProvider.GetRequiredService<IRecommendationSource>());
        Assert.Same(adapter.Hydrator, scope.ServiceProvider.GetRequiredService<ICandidateHydrator>());
        Assert.Same(adapter.SeedTags, scope.ServiceProvider.GetRequiredService<ISeedTagSource>());
    }

    [Fact]
    public void LastFmLiveRemainsBlockedInsideAdapterRegistration()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Generation:Provider"] = "lastfm", ["Generation:Origin"] = "Live", ["Generation:LiveEnabled"] = "true"
        }).Build();
        using var services = new ServiceCollection().AddLogging().AddInfrastructure(config).BuildServiceProvider();
        using var scope = services.CreateScope();

        services.GetRequiredService<GenerationConfiguration>().Scoring();
        Assert.Throws<GenerationStoppedException>(() => scope.ServiceProvider.GetRequiredService<IRecommendationProviderResolver>().Resolve());
        Assert.Throws<GenerationStoppedException>(() => scope.ServiceProvider.GetRequiredService<ILastFmApiClient>());
    }

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
        // Unknown identifiers are rejected by adapter registration; the core validates only their shape.
        if (provider != "unknown")
            Assert.Throws<ArgumentException>(() => services.GetRequiredService<GenerationConfiguration>().Scoring());
        using var scope = services.CreateScope();
        Assert.Throws<GenerationStoppedException>(() => scope.ServiceProvider.GetRequiredService<IRecommendationProviderResolver>().Resolve());
    }
}
