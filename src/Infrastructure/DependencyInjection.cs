using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Infrastructure.RecommendationSources.LastFm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LinerNotes.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bound upstream cache to prevent unbounded growth and process memory exhaustion
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 10_000;
            options.CompactionPercentage = 0.20;
        });

        // Bind Last.fm Options
        services.Configure<LastFmOptions>(configuration.GetSection(LastFmOptions.SectionName));

        // Register thread-safe TokenBucketRateLimiter
        services.AddSingleton<LastFmRateLimiter>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LastFmOptions>>().Value;
            return new LastFmRateLimiter(options.RequestsPerSecond);
        });

        // Register Typed HTTP Client for Last.fm API
        services.AddHttpClient<ILastFmApiClient, LastFmApiClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<LastFmOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.DefaultRequestHeaders.Add("User-Agent", options.UserAgent);
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        // Register Recommendation Source & Candidate Hydrator abstractions
        services.AddScoped<IRecommendationSource, LastFmRecommendationSource>();
        services.AddScoped<ICandidateHydrator, LastFmCandidateHydrator>();

        return services;
    }
}
