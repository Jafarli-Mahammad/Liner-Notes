using LinerNotes.Application.Common.Interfaces.Catalog;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.DataAccess.Core;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.DataAccess.Persistence;
using LinerNotes.DataAccess.Persistence.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LinerNotes.DataAccess;

public static class DependencyInjection
{
    /// <summary>Registers PostgreSQL persistence, repositories, scheduling locks, and shared local email protection services.</summary>
    /// <returns>The supplied service collection.</returns>
    /// <exception cref="InvalidOperationException">The DefaultConnection connection string is missing or blank.</exception>
    public static IServiceCollection AddDataAccess(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection must be supplied through local secrets or the environment.");

        services.AddHttpContextAccessor();

        services.AddDbContext<DataContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(DataContext).Assembly.FullName);
                // EF Core Pattern 6: Default to SplitQuery to prevent Cartesian explosion on multi-collection graphs
                npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                // EF Core Pattern 4: Transient failure retry strategy
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            });
        });

        // Register AppDbContext for backwards compatibility and migrations
        services.AddScoped<AppDbContext>(sp =>
        {
            var options = sp.GetRequiredService<DbContextOptions<DataContext>>();
            var httpContext = sp.GetService<IHttpContextAccessor>();
            return new AppDbContext(options, httpContext);
        });

        // Register Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IListenerProfileRepository, ListenerProfileRepository>();

        // Register generic repository open type
        services.AddScoped(typeof(IAsyncRepository<>), typeof(AsyncRepository<>));

        // Register repository implementations
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAccountProfileReader, AccountProfileReader>();
        services.AddScoped<IDiscoveryProfileReader, DiscoveryProfileReader>();
        services.AddScoped<IGuestProfileStore, GuestProfileStore>();
        services.AddScoped<IDiscoveryTasteStore, DiscoveryTasteStore>();
        services.AddScoped<IDiscoveryJobStore, DiscoveryJobStore>();
        services.AddScoped<IDiscoveryBatchStore, DiscoveryBatchStore>();
        services.AddScoped<IDiscoveryReader, DiscoveryReader>();
        services.AddScoped<IPublicEvidenceStore, PublicEvidenceStore>();
        services.AddScoped<IManagedStorageIntegrity, LinerNotes.DataAccess.Persistence.Accounting.ManagedStorageIntegrity>();
        services.AddScoped<ICatalogReader, CatalogReader>();
        services.AddScoped<ICatalogImportStore, CatalogImportStore>();
        services.AddScoped<IDiscoveryWriteCoordinator, DiscoveryWriteCoordinator>();
        services.AddScoped<IWeeklyScheduleStore>(_ => new WeeklyScheduleStore(connectionString));
        services.AddScoped<IWeeklyDigestRepository, WeeklyDigestRepository>();
        services.AddScoped<ITasteSignalRepository, TasteSignalRepository>();
        services.AddScoped<ITrackRepository, TrackRepository>();
        services.AddScoped<IDigestGenerationStore, DigestGenerationStore>();
        services.AddScoped<IUnsubscribeStore, UnsubscribeStore>();
        services.AddSingleton(_ => new LocalUnsubscribeProtection(connectionString));
        services.AddSingleton<IUnsubscribeTokenProtection>(sp => sp.GetRequiredService<LocalUnsubscribeProtection>());
        services.AddSingleton<ILocalEmailFileProtection>(sp => sp.GetRequiredService<LocalUnsubscribeProtection>());
        services.AddSingleton<IAccountEmailLease>(_ => new AccountEmailLease(connectionString));
        services.AddScoped<ILocalDigestCaptureStore>(_ => new LocalDigestCaptureStore(connectionString));

        return services;
    }
}
