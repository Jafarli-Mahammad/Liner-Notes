namespace LinerNotes.Application.Common.Interfaces.Recommendation;

public sealed record RecommendationProvider(string Id, IRecommendationSource Source,
    ICandidateHydrator Hydrator, ISeedTagSource SeedTags);

/// <summary>Resolves one coherent provider for a generation scope; never falls back.</summary>
public interface IRecommendationProviderResolver
{
    RecommendationProvider Resolve();
}
