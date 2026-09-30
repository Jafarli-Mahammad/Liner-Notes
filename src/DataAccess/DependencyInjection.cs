using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.DataAccess.Persistence;
using LinerNotes.DataAccess.Persistence.Repositories;
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
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=linernotes;Username=linernotes_user;Password=postgres";

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            });
        });

        // Register IAppDbContext abstraction
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // Register repository implementations
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IWeeklyDigestRepository, WeeklyDigestRepository>();
        services.AddScoped<ITasteSignalRepository, TasteSignalRepository>();
        services.AddScoped<ITrackRepository, TrackRepository>();

        return services;
    }
}
