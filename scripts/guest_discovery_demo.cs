#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
#:project ../src/DataAccess/DataAccess.csproj
#:project ../src/Infrastructure/Infrastructure.csproj
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using LinerNotes.Application;
using LinerNotes.Application.Common.Interfaces.Catalog;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Features.Catalog;
using LinerNotes.DataAccess;
using LinerNotes.DataAccess.Persistence;
using LinerNotes.Domain.Catalog;
using LinerNotes.Infrastructure;
using LinerNotes.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

// Creates only a new disposable loopback database. No real acquisition or retained database migration.
if (args.Length != 1 || args[0] != "--prepare-synthetic")
    throw new ArgumentException("Use --prepare-synthetic with private environment configuration.");
var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
var target = new NpgsqlConnectionStringBuilder(configuration.GetConnectionString("DefaultConnection"));
var admin = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("LINER_PHASE1_POSTGRES"));
if (!Regex.IsMatch(target.Database ?? "", "^liner_guest_demo_[a-f0-9]{12}$") ||
    target.Host is not ("127.0.0.1" or "localhost" or "::1") || target.Host != admin.Host || target.Port != admin.Port ||
    configuration["Generation:Origin"] != "Synthetic" || configuration.GetValue<bool>("Generation:LiveEnabled"))
    throw new InvalidOperationException("Disposable loopback database and synthetic generation configuration are required.");
var inventoryPath = configuration["GenerationStorage:InventoryPath"];
if (inventoryPath is null || !Path.IsPathFullyQualified(inventoryPath) || File.Exists(inventoryPath))
    throw new InvalidOperationException("A new absolute inventory path is required.");
await using (var connection = new NpgsqlConnection(admin.ConnectionString))
{
    await connection.OpenAsync();
    await using var create = new NpgsqlCommand($"CREATE DATABASE \"{target.Database}\"", connection);
    await create.ExecuteNonQueryAsync();
}
await using var services = new ServiceCollection().AddLogging().AddApplication().AddDataAccess(configuration)
    .AddInfrastructure(configuration).BuildServiceProvider();
await using (var scope = services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
await using (var scope = services.CreateAsyncScope())
{
    var store = scope.ServiceProvider.GetRequiredService<IDigestGenerationStore>();
    await using var ownership = await store.AcquireBatchAsync();
    await scope.ServiceProvider.GetRequiredService<LinerNotes.Application.Common.Interfaces.Discovery.IManagedStorageIntegrity>().VerifyAsync(default);
    await scope.ServiceProvider.GetRequiredService<LocalGenerationStorage>().ReconcileAsync(await store.ReadStorageAsync());
}
Guid Id(string label) => new(SHA256.HashData(Encoding.UTF8.GetBytes("guest-demo-v1:" + label)).AsSpan(0, 16));
string[] names = ["Synthetic Atlas", "Synthetic Ember", "Synthetic Meridian", "Synthetic Echo", "Synthetic Trio"];
string[] tags = ["jazz", "electronic", "indie", "ambient", "post-punk"];
var rows = Enumerable.Range(0, 100).Select(i => new CatalogArtifactRow(Id("artist:" + i), CatalogKind.Artist,
    i < names.Length ? names[i] : $"Synthetic Artist {i:000}", [$"Demo artist {i:000}"], [],
    [new(tags[i % tags.Length], 1)], "Synthetic demo artist")).ToList();
Guid ProviderFixtureId(string text) => new(SHA256.HashData(Encoding.UTF8.GetBytes("listenbrainz-synthetic:" + text)).AsSpan(0, 16));
rows.AddRange(Enumerable.Range(1, 5).Select(i => new CatalogArtifactRow(ProviderFixtureId("artist-" + i), CatalogKind.Artist,
    "Synthetic Ensemble " + i, [], [], [new("electronic", 1)], "Synthetic provider credit")));
rows.AddRange(Enumerable.Range(0, 20).Select(i => new CatalogArtifactRow(Id("album:" + i), CatalogKind.Album,
    i == 0 ? "Synthetic Horizons" : $"Synthetic Album {i:000}", [], [rows[i].Id, rows[(i + 1) % 100].Id],
    [new(tags[i % tags.Length], 1)], "Synthetic demo album", "2000")));
rows.AddRange(Enumerable.Range(1, 5).Select(i => new CatalogArtifactRow(ProviderFixtureId("recording-" + i), CatalogKind.Track,
    "Synthetic Discovery " + i, [], [ProviderFixtureId("artist-" + i)], [new("electronic", 1, "synthetic-recording")],
    "Synthetic studio recording", "2000")));

var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
json.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
var bytes = Encoding.UTF8.GetBytes(string.Join('\n', rows.Select(row => JsonSerializer.Serialize(row, json))) + "\n");
var now = DateTimeOffset.UtcNow;
var manifest = new CatalogPublication(Guid.NewGuid(), "generated-fixture", "guest-demo-v1", "fixture:guest-demo",
    CatalogOrigin.Synthetic, "synthetic-fixture", "synthetic-fixture", "Synthetic mechanics only",
    Convert.ToHexStringLower(SHA256.HashData(bytes)), bytes.Length, rows.Count, now, now.AddDays(7));
await using (var scope = services.CreateAsyncScope())
{
    using var artifact = new MemoryStream(bytes, writable: false);
    await scope.ServiceProvider.GetRequiredService<ImportCatalog>().RunAsync(manifest, artifact, false, false, default);
}
Console.WriteLine(JsonSerializer.Serialize(new { database = target.Database, catalogRecords = rows.Count,
    origin = "Synthetic", note = "Mechanics only; no real catalog or quality evidence." }));
