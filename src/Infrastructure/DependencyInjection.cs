using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Infrastructure.Email;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Infrastructure.RecommendationSources.LastFm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Infrastructure.Storage;

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

        services.Configure<LocalEmailOptions>(configuration.GetSection("LocalEmail"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<LocalEmailOptions>>().Value);
        services.AddSingleton<IUnsubscribeTokens, UnsubscribeTokenService>();
        services.AddScoped<IDigestEmailRenderer, DigestEmailRenderer>();
        services.AddScoped<LocalEmailMessageSerializer>();
        services.AddScoped<ILocalEmailSink, LocalEmailSink>();
        services.AddScoped<ILocalEmailArchive, LocalEmailArchive>();

        // Bind Last.fm Options
        services.Configure<LastFmOptions>(configuration.GetSection(LastFmOptions.SectionName));

        // Register thread-safe TokenBucketRateLimiter
        services.AddSingleton<LastFmRateLimiter>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LastFmOptions>>().Value;
            return new LastFmRateLimiter(options.RequestsPerSecond);
        });

        // Register Typed HTTP Client for Last.fm API
        services.AddHttpClient<LastFmApiClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<LastFmOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.DefaultRequestHeaders.Add("User-Agent", options.UserAgent);
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.Configure<GenerationConfiguration>(configuration.GetSection(GenerationConfiguration.SectionName));
        services.Configure<GenerationStorageOptions>(configuration.GetSection("GenerationStorage"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<GenerationConfiguration>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<GenerationStorageOptions>>().Value);
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<BatchTagCachingLastFmClient>(sp =>
        {
            var generation = sp.GetRequiredService<GenerationConfiguration>();
            generation.Scoring(); // Live is explicitly rejected before resolving a provider.
            ILastFmApiClient client = generation.Origin == EvidenceOrigin.Recorded
                ? new RecordedLastFmApiClient(sp.GetServices<RecordedLastFmResponse>())
                : new LastFmApiClient(new HttpClient(), Options.Create(new LastFmOptions { Mode = LastFmClientMode.FixtureOnly }),
                    sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(), sp.GetRequiredService<LastFmRateLimiter>(),
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LastFmApiClient>>());
            return new(client);
        });
        services.AddScoped<ILastFmApiClient>(sp => sp.GetRequiredService<BatchTagCachingLastFmClient>());
        services.AddScoped<ISeedTagSource>(sp => sp.GetRequiredService<BatchTagCachingLastFmClient>());
        services.AddScoped<LocalGenerationStorage>();
        services.AddScoped<IGenerationStorage>(sp => sp.GetRequiredService<LocalGenerationStorage>());
        services.AddScoped<ILocalEmailStorage>(sp => sp.GetRequiredService<LocalGenerationStorage>());

        // Register Recommendation Source & Candidate Hydrator abstractions
        services.AddScoped<IRecommendationSource, LastFmRecommendationSource>();
        services.AddScoped<ICandidateHydrator, LastFmCandidateHydrator>();

        return services;
    }
}
