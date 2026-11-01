using LinerNotes.Application;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.Infrastructure;
using LinerNotes.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed partial class DiscoveryCoordinationPostgresTests
{
    [LocalPostgresMigrationFact]
    public async Task RuntimeRoleCanUseGuardedStorageButCannotCreateSchemaObjects()
    {
        await EnableDurableProtocolAsync();
        var role = "liner_role_" + Guid.NewGuid().ToString("N");
        var password = Guid.NewGuid().ToString("N");
        await using var owner = new NpgsqlConnection(connection);
        await owner.OpenAsync();
        await using (var grant = new NpgsqlCommand($"""
            CREATE ROLE "{role}" LOGIN PASSWORD '{password}';
            REVOKE CREATE ON SCHEMA public FROM PUBLIC;
            GRANT USAGE ON SCHEMA public,identity TO "{role}";
            GRANT SELECT,INSERT,UPDATE,DELETE ON ALL TABLES IN SCHEMA public,identity TO "{role}";
            GRANT USAGE,SELECT ON ALL SEQUENCES IN SCHEMA public,identity TO "{role}";
            """, owner)) await grant.ExecuteNonQueryAsync();
        try
        {
            var settings = new NpgsqlConnectionStringBuilder(connection) { Username = role, Password = password, Pooling = false };
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["ConnectionStrings:DefaultConnection"] = settings.ConnectionString, ["LastFm:Mode"] = "FixtureOnly"
            }).Build();
            var runtime = new ServiceCollection().AddLogging().AddApplication().AddDataAccess(config).AddInfrastructure(config);
            runtime.AddSingleton(new GenerationStorageOptions
            {
                InventoryPath = Path.Combine(root,"inventory.json"), LeasePath = Path.Combine(root,"lease"),
                DatabaseOverheadBytesPerPick = 1_048_576,
                ArtifactRoots = LocalGenerationStorage.Categories.ToDictionary(k => k, _ => Array.Empty<string>())
            });
            runtime.AddSingleton(new CoordinatedStorageOptions { FilesystemPaths = [root], MinimumFreeBytes = 67_108_864 });
            runtime.AddSingleton(new DiscoveryRuntimeOptions { EnforceStorageAdmission = true });
            await using var provider = runtime.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var profile = await scope.ServiceProvider.GetRequiredService<GuestLifecycle>().StartAsync(new string('c',64), default);
            Assert.NotEqual(Guid.Empty,profile.Id);
            await scope.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
            await using var restricted = new NpgsqlConnection(settings.ConnectionString);
            await restricted.OpenAsync();
            var refused = await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await using var ddl = new NpgsqlCommand("CREATE TABLE public.forbidden_role_test(id integer)",restricted);
                await ddl.ExecuteNonQueryAsync();
            });
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege,refused.SqlState);
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand($"DROP OWNED BY \"{role}\"; DROP ROLE \"{role}\"",owner);
            await cleanup.ExecuteNonQueryAsync();
        }
    }
}
