using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Infrastructure.Email;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Infrastructure.RecommendationSources.LastFm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Infrastructure.Storage;
using LinerNotes.Infrastructure.RecommendationSources;
using LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;

namespace LinerNotes.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers recommendation providers, bounded caching, generation storage, and local email capture, archive, and conversion services.</summary>
    /// <returns>The supplied service collection.</returns>
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

        services.Configure<LinerNotes.Infrastructure.Caching.DiscoveryCacheOptions>(configuration.GetSection("DiscoveryCache"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<LinerNotes.Infrastructure.Caching.DiscoveryCacheOptions>>().Value);
        services.AddSingleton<LinerNotes.Infrastructure.Caching.DiscoveryMemoryCaches>();
        services.Configure<LocalRecordedInputApproval>(configuration.GetSection("LocalRecordedInput"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<LocalRecordedInputApproval>>().Value);
        services.AddSingleton<LocalRecordedInputs>();
        services.AddSingleton<LocalRecordedInputLoader>();
        services.Configure<LocalEmailOptions>(configuration.GetSection("LocalEmail"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<LocalEmailOptions>>().Value);
        services.AddSingleton<IUnsubscribeTokens, UnsubscribeTokenService>();
        services.AddScoped<IDigestEmailRenderer, DigestEmailRenderer>();
        services.AddScoped<LocalEmailMessageSerializer>();
        services.AddScoped<LocalEmailLegacyConversion>();
        services.AddScoped<ILocalEmailSink, LocalEmailSink>();
        services.AddScoped<LocalEmailArchive>();
        services.AddScoped<ILocalEmailArchive>(sp => sp.GetRequiredService<LocalEmailArchive>());

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

        services.Configure<LinerNotes.Application.Features.Discovery.DiscoveryTasteOptions>(configuration.GetSection("DiscoveryTaste"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<LinerNotes.Application.Features.Discovery.DiscoveryTasteOptions>>().Value);
        services.Configure<GenerationConfiguration>(configuration.GetSection(GenerationConfiguration.SectionName));
        services.Configure<GenerationStorageOptions>(configuration.GetSection("GenerationStorage"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<GenerationConfiguration>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<GenerationStorageOptions>>().Value);
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<BatchTagCachingLastFmClient>(sp =>
        {
            var generation = sp.GetRequiredService<GenerationConfiguration>();
            generation.Scoring();
            if (generation.Origin == EvidenceOrigin.Live)
                throw new GenerationStoppedException("lastfm_live_generation_not_approved");
            ILastFmApiClient client = generation.Origin == EvidenceOrigin.Recorded
                ? new RecordedLastFmApiClient(sp.GetRequiredService<LocalRecordedInputs>().Responses ?? sp.GetServices<RecordedLastFmResponse>())
                : new LastFmApiClient(new HttpClient(), Options.Create(new LastFmOptions { Mode = LastFmClientMode.FixtureOnly }),
                    sp.GetRequiredService<LinerNotes.Infrastructure.Caching.DiscoveryMemoryCaches>().LastFm, sp.GetRequiredService<LastFmRateLimiter>(),
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LastFmApiClient>>());
            return new(client);
        });
        services.AddScoped<ILastFmApiClient>(sp => sp.GetRequiredService<BatchTagCachingLastFmClient>());
        services.AddScoped<ISeedTagSource>(sp => sp.GetRequiredService<IRecommendationProviderResolver>().Resolve().SeedTags);
        services.AddSingleton<IStorageFreeSpaceProbe, DriveStorageFreeSpaceProbe>();
        services.AddScoped<LocalGenerationStorage>();
        services.Configure<CoordinatedStorageOptions>(configuration.GetSection("DiscoveryStorage"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<CoordinatedStorageOptions>>().Value);
        services.AddScoped<LinerNotes.Application.Common.Interfaces.Discovery.ICoordinatedGenerationStorage>(sp => sp.GetRequiredService<LocalGenerationStorage>());
        services.AddScoped<IGenerationStorage>(sp => sp.GetRequiredService<LocalGenerationStorage>());
        services.AddScoped<ILocalEmailStorage>(sp => sp.GetRequiredService<LocalGenerationStorage>());

        // Register Recommendation Source & Candidate Hydrator abstractions
        services.AddScoped<LastFmRecommendationSource>();
        services.AddScoped<LastFmCandidateHydrator>();
        services.Configure<ListenBrainzOptions>(configuration.GetSection(ListenBrainzOptions.SectionName));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<ListenBrainzOptions>>().Value);
        services.AddSingleton<IMetaBrainzRequestGate, MetaBrainzRequestGate>();
        services.AddSingleton<LinerNotes.Infrastructure.Artwork.RecordingArtworkCache>();
        services.AddScoped<LinerNotes.Application.Common.Interfaces.Discovery.IRecordingArtwork, LinerNotes.Infrastructure.Artwork.RecordingArtwork>();
        services.AddHttpClient("CoverArtArchive", http => http.Timeout = TimeSpan.FromSeconds(15)).RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        services.AddHttpClient<ListenBrainzApiClient>(http => http.Timeout = TimeSpan.FromSeconds(15)).RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        services.AddScoped<IListenBrainzApiClient>(sp =>
        {
            var generation = sp.GetRequiredService<GenerationConfiguration>();
            var options = sp.GetRequiredService<ListenBrainzOptions>();
            options.Validate();
            return generation.Origin switch
            {
                EvidenceOrigin.Synthetic when options.Mode == ListenBrainzClientMode.FixtureOnly => new FixtureListenBrainzApiClient(),
                EvidenceOrigin.Recorded => new RecordedListenBrainzApiClient(sp.GetRequiredService<LocalRecordedInputs>().ListenBrainzResponses ?? sp.GetServices<RecordedListenBrainzResponse>()),
                EvidenceOrigin.Live when generation.LiveEnabled && options.Mode == ListenBrainzClientMode.LiveOnly && options.LiveUseApproved
                    => sp.GetRequiredService<ListenBrainzApiClient>(),
                _ => throw new GenerationStoppedException("listenbrainz_mode_origin_mismatch")
            };
        });
        services.AddScoped<ListenBrainzRecommendationProvider>();
        services.AddKeyedScoped<RecommendationProvider>("lastfm", (sp, _) =>
            new("lastfm", sp.GetRequiredService<LastFmRecommendationSource>(),
                sp.GetRequiredService<LastFmCandidateHydrator>(), sp.GetRequiredService<BatchTagCachingLastFmClient>()));
        services.AddKeyedScoped<RecommendationProvider>("listenbrainz", (sp, _) =>
        {
            var provider = sp.GetRequiredService<ListenBrainzRecommendationProvider>();
            return new("listenbrainz", provider, provider, provider);
        });
        services.AddScoped<LinerNotes.Application.Common.Interfaces.Catalog.ICatalogLookupSource, LinerNotes.Infrastructure.Catalog.MusicBrainzCatalogLookupSource>();
        services.AddScoped<IRecommendationProviderResolver, RecommendationProviderResolver>();
        services.Configure<LinerNotes.Application.Features.Discovery.DiscoveryRuntimeOptions>(configuration.GetSection("DiscoveryRuntime"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<LinerNotes.Application.Features.Discovery.DiscoveryRuntimeOptions>>().Value);
        services.AddSingleton<LinerNotes.Infrastructure.Discovery.SharedPublicEvidence>();
        services.AddSingleton<LinerNotes.Application.Common.Interfaces.Catalog.ICatalogLookupPreparation, LinerNotes.Infrastructure.Discovery.SharedCatalogLookup>();
        services.AddSingleton<IDiscoverySeedPreparation, LinerNotes.Infrastructure.Discovery.SharedSeedPreparation>();
        services.AddSingleton<LinerNotes.Infrastructure.Discovery.SharedDiscoveryPreparation>();
        services.AddSingleton<IDiscoveryPreparation, LinerNotes.Infrastructure.Discovery.PersistentDiscoveryPreparation>();
        services.AddScoped<IRecommendationSource>(sp => sp.GetRequiredService<IRecommendationProviderResolver>().Resolve().Source);
        services.AddScoped<ICandidateHydrator>(sp => sp.GetRequiredService<IRecommendationProviderResolver>().Resolve().Hydrator);

        services.AddSingleton<LinerNotes.Application.Common.Interfaces.Catalog.ICatalogArtifactReader, LinerNotes.Infrastructure.Catalog.CatalogArtifactReader>();
        return services;
    }
}
