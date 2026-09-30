using LinerNotes.DataAccess.Persistence;
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
            ?? "Data Source=linernotes.db";

        services.AddDbContext<AppDbContext>(options =>
        {
            // Default in-memory/sqlite provider setup placeholder for V1
            // Provider (Npgsql or Sqlite) can be configured via connection string
            options.UseSqlite(connectionString);
        });

        return services;
    }
}
