using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Repositories;
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

        // Register IAppDbContext abstraction
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<DataContext>());

        // Register Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Register generic repository open type
        services.AddScoped(typeof(IAsyncRepository<>), typeof(AsyncRepository<>));

        // Register repository implementations
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IWeeklyDigestRepository, WeeklyDigestRepository>();
        services.AddScoped<ITasteSignalRepository, TasteSignalRepository>();
        services.AddScoped<ITrackRepository, TrackRepository>();

        return services;
    }
}
