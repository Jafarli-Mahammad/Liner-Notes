#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:project ../src/DataAccess/DataAccess.csproj
#:project ../src/Infrastructure/Infrastructure.csproj

using LinerNotes.Application;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.DataAccess;
using LinerNotes.DataAccess.Persistence;
using LinerNotes.Infrastructure;
using LinerNotes.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Explicit operator establishment, never an automatic startup repair. Stop ALL writers first.
if (args is not ["--establish", "--writers-stopped", "--configuration", var path] || !Path.IsPathFullyQualified(path))
    throw new ArgumentException("Use --establish --writers-stopped --configuration ABSOLUTE_PRIVATE_JSON_PATH.");
try
{
    // An explicitly selected private configuration must win over an unrelated shell's settings.
    var configuration = new ConfigurationBuilder().AddEnvironmentVariables().AddJsonFile(path, optional: false).Build();
    await using var services = new ServiceCollection().AddLogging().AddApplication().AddDataAccess(configuration)
        .AddInfrastructure(configuration).BuildServiceProvider();
    await using var scope = services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if ((await db.Database.GetPendingMigrationsAsync()).Any())
        throw new GenerationStoppedException("storage_migrations_pending");
    var store = scope.ServiceProvider.GetRequiredService<IDigestGenerationStore>();
    await using var admission = await store.AcquireBatchAsync();
    var publications = await scope.ServiceProvider.GetRequiredService<IArtifactPublicationStore>().ReadActiveAsync(default);
    if (publications.Count != 0)
        throw new GenerationStoppedException("storage_pending_publications_require_recovery_before_establishment");
    await scope.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
    await scope.ServiceProvider.GetRequiredService<LocalGenerationStorage>().ReconcileAsync(await store.ReadStorageAsync());
    Console.WriteLine("{\"status\":\"established\"}");
    return 0;
}
catch (GenerationStoppedException error)
{
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { status = "stopped", reason = error.Message }));
    return 2;
}
catch
{
    Console.WriteLine("{\"status\":\"stopped\",\"reason\":\"storage_configuration_database_or_io\"}");
    return 2;
}
