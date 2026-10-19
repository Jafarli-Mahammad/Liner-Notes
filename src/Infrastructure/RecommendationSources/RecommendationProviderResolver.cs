using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using Microsoft.Extensions.DependencyInjection;

namespace LinerNotes.Infrastructure.RecommendationSources;

internal sealed class RecommendationProviderResolver(IServiceProvider services, GenerationConfiguration configuration)
    : IRecommendationProviderResolver
{
    private RecommendationProvider? resolved;

    public RecommendationProvider Resolve() => resolved ??=
        services.GetKeyedService<RecommendationProvider>(configuration.Provider)
        ?? throw new GenerationStoppedException("recommendation_provider_invalid");
}
