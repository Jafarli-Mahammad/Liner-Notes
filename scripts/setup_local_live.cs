#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:project ../src/DataAccess/DataAccess.csproj
#:project ../src/Infrastructure/Infrastructure.csproj

using System.Text.Json;
using LinerNotes.Application;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.DataAccess;
using LinerNotes.DataAccess.Persistence;
using LinerNotes.Domain.Catalog;
using LinerNotes.Infrastructure;
using LinerNotes.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

if (args is not ["--initialize"])
    throw new ArgumentException("Use --initialize from the repository root to prepare the persistent local live runtime.");
if (!File.Exists("src/Presentation/Presentation.csproj"))
    throw new InvalidOperationException("Run this command from the repository root.");

var root = Path.GetFullPath(".linernotes/live");
var settingsPath = Path.Combine(root, "settings.json");
var privateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
void PrivateDirectory(string path)
{
    if (OperatingSystem.IsWindows()) Directory.CreateDirectory(path);
    else Directory.CreateDirectory(path, privateMode);
    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        throw new InvalidOperationException("Live runtime directories must not be symbolic links.");
}
PrivateDirectory(Path.GetDirectoryName(root)!);
PrivateDirectory(root);

var source = new ConfigurationBuilder().AddUserSecrets("linernotes-secrets-app-v1").Build();
var existing = File.Exists(settingsPath);
var settings = existing
    ? JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(settingsPath))!
    : new Dictionary<string, string>
    {
        ["ConnectionStrings:DefaultConnection"] = new NpgsqlConnectionStringBuilder(source.GetConnectionString("DefaultConnection")) { Database = "liner_notes_live" }.ConnectionString,
        ["Jwt:SecretKey"] = source["Jwt:SecretKey"] ?? throw new InvalidOperationException("Configure Jwt:SecretKey in Presentation User Secrets."),
        ["Generation:Provider"] = "listenbrainz",
        ["Generation:Origin"] = "Live",
        ["Generation:LiveEnabled"] = "true",
        ["ListenBrainz:Mode"] = "LiveOnly",
        ["ListenBrainz:LiveUseApproved"] = "true",
        ["ListenBrainz:Token"] = source["ListenBrainz:Token"] ?? throw new InvalidOperationException("Configure ListenBrainz:Token in Presentation User Secrets."),
        ["ListenBrainz:RateLimitDirectory"] = Path.Combine(root, "pacing"),
        ["GuestDiscovery:Enabled"] = "true",
        ["GuestDiscovery:PublicOrigin"] = "http://localhost:5080",
        ["DiscoveryRuntime:RunJobs"] = "true",
        ["LocalEmail:Enabled"] = "false",
        ["GenerationStorage:InventoryPath"] = Path.Combine(root, "inventory.json"),
        ["GenerationStorage:LeasePath"] = Path.Combine(root, "lease.lock"),
        ["GenerationStorage:DatabaseOverheadBytesPerPick"] = "1048576",
        ["DiscoveryStorage:FilesystemPaths:0"] = root,
        ["Logging:LogLevel:Default"] = "Information",
        ["Logging:LogLevel:Microsoft"] = "Warning",
        ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning",
        ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] = "Warning",
        ["Logging:LogLevel:Microsoft.Hosting.Lifetime"] = "Information"
    };
foreach (var category in LocalGenerationStorage.Categories)
{
    var path = Path.Combine(root, category);
    PrivateDirectory(path);
    settings[$"GenerationStorage:ArtifactRoots:{category}:0"] = path;
}
PrivateDirectory(settings["ListenBrainz:RateLimitDirectory"]);
var target = new NpgsqlConnectionStringBuilder(settings["ConnectionStrings:DefaultConnection"]);
if (target.Host is not ("localhost" or "127.0.0.1" or "::1") || target.Database != "liner_notes_live" ||
    settings["Generation:Origin"] != "Live" || settings["ListenBrainz:Mode"] != "LiveOnly")
    throw new InvalidOperationException("Only the dedicated loopback live database and live provider configuration are supported.");
var admin = new NpgsqlConnectionStringBuilder(target.ConnectionString) { Database = "postgres" };
bool created;
await using (var connection = new NpgsqlConnection(admin.ConnectionString))
{
    await connection.OpenAsync();
    await using var check = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name)", connection);
    check.Parameters.AddWithValue("name", target.Database);
    created = !(bool)(await check.ExecuteScalarAsync())!;
    if (created)
    {
        await using var create = new NpgsqlCommand("CREATE DATABASE liner_notes_live", connection);
        await create.ExecuteNonQueryAsync();
    }
    else if (!existing || !File.Exists(settings["GenerationStorage:InventoryPath"]))
        throw new InvalidOperationException("An existing live database requires its matching private settings and inventory. Restore those files before continuing.");
}

var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value))).Build();
await using var services = new ServiceCollection().AddLogging().AddApplication().AddDataAccess(configuration)
    .AddInfrastructure(configuration).BuildServiceProvider();
await using (var scope = services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (created) await database.Database.MigrateAsync();
    else if ((await database.Database.GetPendingMigrationsAsync()).Any())
        throw new InvalidOperationException("The retained live database needs an explicit migration with writers stopped and a backup.");
    if (await database.Set<CatalogPublication>().AnyAsync(item => item.Origin != CatalogOrigin.Public))
        throw new InvalidOperationException("The live database contains ineligible catalog data.");
}
await using (var scope = services.CreateAsyncScope())
{
    if (created)
    {
        var store = scope.ServiceProvider.GetRequiredService<IDigestGenerationStore>();
        await using var lease = await store.AcquireBatchAsync();
        await scope.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
        await scope.ServiceProvider.GetRequiredService<LocalGenerationStorage>().ReconcileAsync(await store.ReadStorageAsync());
    }
    else await scope.ServiceProvider.GetRequiredService<MaintainDiscoveryStorage>().ExecuteAsync(default);
}
if (!existing)
{
    var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
    if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    await using var file = new FileStream(settingsPath, options);
    await JsonSerializer.SerializeAsync(file, settings, new JsonSerializerOptions { WriteIndented = true });
}
Console.WriteLine("Persistent live runtime initialized. No synthetic data was inserted. Run bash scripts/run_live.sh and open http://localhost:5080/.");
