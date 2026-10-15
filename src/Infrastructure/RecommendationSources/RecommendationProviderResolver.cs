using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Infrastructure.RecommendationSources.LastFm;
using LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;
using Microsoft.Extensions.DependencyInjection;

namespace LinerNotes.Infrastructure.RecommendationSources;

internal sealed class RecommendationProviderResolver(IServiceProvider services, GenerationConfiguration configuration)
    : IRecommendationProviderResolver
{
    private RecommendationProvider? resolved;

    public RecommendationProvider Resolve() => resolved ??= configuration.Provider switch
    {
        "lastfm" => new("lastfm", services.GetRequiredService<LastFmRecommendationSource>(),
            services.GetRequiredService<LastFmCandidateHydrator>(), services.GetRequiredService<BatchTagCachingLastFmClient>()),
        "listenbrainz" => ListenBrainzBundle(),
        _ => throw new GenerationStoppedException("recommendation_provider_invalid")
    };

    private RecommendationProvider ListenBrainzBundle()
    {
        var provider = services.GetRequiredService<ListenBrainzRecommendationProvider>();
        return new("listenbrainz", provider, provider, provider);
    }
}
