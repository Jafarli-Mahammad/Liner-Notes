using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinerNotes.Application;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Catalog;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Features.Catalog;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.DataAccess.Persistence;
using LinerNotes.DataAccess.Persistence.Repositories;
using LinerNotes.Domain.Catalog;
using LinerNotes.Infrastructure;
using LinerNotes.Infrastructure.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed partial class CatalogPostgresTests : IAsyncLifetime
{
    private readonly string _name = "liner_catalog_c5_" + Guid.NewGuid().ToString("N");
    private string _admin = "";
    private ServiceProvider _services = null!;
    private static readonly DateTimeOffset Acquired = new(2026, 10, 16, 0, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync()
    {
        _admin = Environment.GetEnvironmentVariable("LINER_PHASE1_POSTGRES") ?? "";
        if (_admin.Length == 0) return;
        var settings = new NpgsqlConnectionStringBuilder(_admin);
        Assert.Contains(settings.Host, new[] { "localhost", "127.0.0.1", "::1" });
        await using (var connection = new NpgsqlConnection(_admin))
        {
            await connection.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_name}\"", connection);
            await create.ExecuteNonQueryAsync();
        }
        settings.Database = _name;
        settings.Pooling = false;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:DefaultConnection"] = settings.ConnectionString, ["LastFm:Mode"] = "FixtureOnly" }).Build();
        var services = new ServiceCollection().AddLogging().AddApplication().AddDataAccess(configuration).AddInfrastructure(configuration);
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IDiscoveryWriteCoordinator, TransactionalAdmission>();
        services.AddSingleton<ICatalogLookupSource, DenyLookup>();
        _services = services.BuildServiceProvider();
        await using var scope = _services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_services is not null) await _services.DisposeAsync();
        if (_admin.Length == 0) return;
        await using var connection = new NpgsqlConnection(_admin);
        await connection.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_name}\" WITH (FORCE)", connection);
        await drop.ExecuteNonQueryAsync();
    }

    [LocalPostgresMigrationFact]
    public async Task SearchFindsAliasesAccentsNonLatinAndMultiArtistAlbumsWithoutProviderCalls()
    {
        await using var scope = _services.CreateAsyncScope();
        var artist = Guid.NewGuid(); var other = Guid.NewGuid(); var album = Guid.NewGuid(); var punctuation = Guid.NewGuid();
        CatalogArtifactRow[] rows = [new(artist, CatalogKind.Artist, "Synthetic Björk", ["Fixture alias"], [], [new("jazz", 1)]),
            new(other, CatalogKind.Artist, "İşıq Əlif 東京", [], [], []),
            new(punctuation, CatalogKind.Artist, "!!!", [], [], []),
            new(album, CatalogKind.Album, "Synthetic Night Album", [], [artist, other], [new("electronic", 0.8)], "Two credited artists", "2000-01-01")];
        await ImportAsync(scope.ServiceProvider, rows);
        var reader = scope.ServiceProvider.GetRequiredService<ICatalogReader>();
        Assert.Equal(artist, Assert.Single((await reader.SearchAsync("bjork", CatalogKind.Artist, 10, 0, DateTimeOffset.UtcNow, default)).Items).CanonicalId);
        Assert.Equal(artist, Assert.Single((await reader.SearchAsync("fixture alia", CatalogKind.Artist, 10, 0, DateTimeOffset.UtcNow, default)).Items).CanonicalId);
        Assert.Equal(other, Assert.Single((await reader.SearchAsync("isiq elif", CatalogKind.Artist, 10, 0, DateTimeOffset.UtcNow, default)).Items).CanonicalId);
        Assert.Equal(other, Assert.Single((await reader.SearchAsync("東京", CatalogKind.Artist, 10, 0, DateTimeOffset.UtcNow, default)).Items).CanonicalId);
        Assert.Equal(punctuation, Assert.Single((await reader.SearchAsync("!!!", CatalogKind.Artist, 10, 0, DateTimeOffset.UtcNow, default)).Items).CanonicalId);
        var item = Assert.Single((await reader.SearchAsync("night", CatalogKind.Album, 10, 0, DateTimeOffset.UtcNow, default)).Items);
        Assert.Equal(album, item.CanonicalId); Assert.Equal("Two credited artists", item.Disambiguation);
        Assert.Equal(new[] { artist, other }, item.Artists!.OrderBy(c => c.Position).Select(c => c.CanonicalId));
        Assert.Equal("synthetic-fixture", item.MetadataLicense);
        Assert.Contains("Synthetic Björk", item.ArtistName); Assert.Contains("İşıq Əlif 東京", item.ArtistName);
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        Assert.Equal(2, await db.AlbumCredits.CountAsync(c => c.AlbumId == album));
        Assert.Equal(album, (await db.Albums.SingleAsync(a => a.Id == album)).ReleaseGroupMbid);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [LocalPostgresMigrationFact]
    public async Task SearchPaginationTyposAndBoundsAreDeterministic()
    {
        await using var scope = _services.CreateAsyncScope();
        var rows = Enumerable.Range(1, 26).Select(i => new CatalogArtifactRow(Guid.NewGuid(), CatalogKind.Artist,
            $"Synthetic Zephyr {i:00}", [], [], [])).ToArray();
        await ImportAsync(scope.ServiceProvider, rows);
        var reader = scope.ServiceProvider.GetRequiredService<ICatalogReader>();
        var first = await reader.SearchAsync("zephyr", CatalogKind.Artist, 10, 0, DateTimeOffset.UtcNow, default);
        var second = await reader.SearchAsync("zephyr", CatalogKind.Artist, 10, 10, DateTimeOffset.UtcNow, default);
        Assert.Equal(10, first.Items.Count); Assert.True(first.HasMore);
        Assert.Empty(first.Items.Select(r => r.Id).Intersect(second.Items.Select(r => r.Id)));
        Assert.Equal(first.Items.Select(r => r.Id), (await reader.SearchAsync("zephyr", CatalogKind.Artist, 10, 0, DateTimeOffset.UtcNow, default)).Items.Select(r => r.Id));
        Assert.NotEmpty((await reader.SearchAsync("zephry", CatalogKind.Artist, 10, 0, DateTimeOffset.UtcNow, default)).Items);
        Assert.Empty((await reader.SearchAsync("%_", CatalogKind.Artist, 10, 0, DateTimeOffset.UtcNow, default)).Items);
        await Assert.ThrowsAsync<ArgumentException>(() => reader.SearchAsync("z", CatalogKind.Artist, 1000, 0, DateTimeOffset.UtcNow, default));
        await Assert.ThrowsAsync<ArgumentException>(() => reader.SearchAsync("z", CatalogKind.Artist, 10, 1000, DateTimeOffset.UtcNow, default));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.SearchAsync("zephyr", CatalogKind.Artist, 10, 0, DateTimeOffset.UtcNow, cancelled.Token));
    }

    [LocalPostgresMigrationFact]
    public async Task InterruptedImportResumesAndDoesNotReplaceTheLastGoodPublicationEarly()
    {
        await using var scope = _services.CreateAsyncScope();
        var original = new CatalogArtifactRow(Guid.NewGuid(), CatalogKind.Artist, "Original synthetic favorite", [], [], []);
        await ImportAsync(scope.ServiceProvider, [original]);
        var rows = Enumerable.Range(0, 251).Select(i => new CatalogArtifactRow(Guid.NewGuid(), CatalogKind.Artist, $"Replacement synthetic {i:000}", [], [], [])).ToArray();
        using var artifact = Artifact(rows);
        var manifest = Manifest(artifact, rows.Length);
        var importer = scope.ServiceProvider.GetRequiredService<ImportCatalog>();
        var admission = (TransactionalAdmission)scope.ServiceProvider.GetRequiredService<IDiscoveryWriteCoordinator>();
        admission.AllowNextWrites(2); // Start and first 250-row chunk commit; next chunk fails before writing.
        await Assert.ThrowsAsync<IOException>(() => importer.RunAsync(manifest, artifact, false, false, default));
        var store = scope.ServiceProvider.GetRequiredService<ICatalogImportStore>();
        Assert.Equal(250, (await store.FindAsync(manifest.Id, default))!.ImportedRecords);
        var reader = scope.ServiceProvider.GetRequiredService<ICatalogReader>();
        Assert.Single((await reader.SearchAsync("original", CatalogKind.Artist, 10, 0, DateTimeOffset.UtcNow, default)).Items);
        Assert.Empty((await reader.SearchAsync("replacement", CatalogKind.Artist, 10, 0, DateTimeOffset.UtcNow, default)).Items);
        admission.FailAfter = int.MaxValue;
        await importer.RunAsync(manifest, artifact, false, false, default);
        Assert.Empty((await reader.SearchAsync("original", CatalogKind.Artist, 10, 0, DateTimeOffset.UtcNow, default)).Items);
        Assert.NotEmpty((await reader.SearchAsync("replacement", CatalogKind.Artist, 10, 0, DateTimeOffset.UtcNow, default)).Items);
        Assert.Equal(CatalogPublicationState.Published, (await store.FindAsync(manifest.Id, default))!.State);
        Assert.Empty(scope.ServiceProvider.GetRequiredService<DataContext>().ChangeTracker.Entries());
    }

    [LocalPostgresMigrationFact]
    public async Task HashFailureAndDisabledLookupDoNotWriteOrQueueWork()
    {
        await using var scope = _services.CreateAsyncScope();
        using var artifact = Artifact([new(Guid.NewGuid(), CatalogKind.Artist, "Synthetic hash fixture", [], [], [])]);
        var bad = new CatalogPublication(Guid.NewGuid(), "generated-fixture", "fixture-v1", "fixture:catalog", CatalogOrigin.Synthetic,
            "synthetic-fixture", null, "Synthetic mechanics only", new string('a', 64), artifact.Length, 1, Acquired);
        await Assert.ThrowsAsync<ArgumentException>(() => scope.ServiceProvider.GetRequiredService<ImportCatalog>().RunAsync(bad, artifact, false, false, default));
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        Assert.False(await db.CatalogPublications.AnyAsync());
        var handler = new SubmitCatalogLookupHandler(scope.ServiceProvider.GetRequiredService<ICatalogLookupJobs>());
        await Assert.ThrowsAsync<LinerNotes.Application.Common.Interfaces.Recommendation.GenerationStoppedException>(() =>
            handler.Handle(new(Guid.NewGuid(), "Any missing favorite", CatalogKind.Album, "explicit-request"), default));
        Assert.False(await db.DiscoveryJobs.AnyAsync());
    }

    [LocalPostgresMigrationFact]
    public async Task ResumeRejectsACommittedRowThatDoesNotMatchTheApprovedArtifact()
    {
        await using var scope = _services.CreateAsyncScope();
        var approved = new CatalogArtifactRow(Guid.NewGuid(), CatalogKind.Artist, "Approved synthetic row", [], [], []);
        using var artifact = Artifact([approved]);
        var manifest = Manifest(artifact, 1);
        var admission = scope.ServiceProvider.GetRequiredService<IDiscoveryWriteCoordinator>();
        var store = scope.ServiceProvider.GetRequiredService<ICatalogImportStore>();
        await admission.ExecuteAsync(65_536, async ct => { await store.StartAsync(manifest, ct); return true; }, default);
        await admission.ExecuteAsync(65_536, async ct =>
        {
            await store.AppendAsync(manifest.Id, 0, [approved with { Name = "Injected synthetic row" }], ct);
            return true;
        }, default);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<ImportCatalog>()
            .RunAsync(manifest, artifact, false, false, default));
        Assert.Equal("catalog_resume_row_integrity_mismatch", error.Message);
        Assert.Equal(CatalogPublicationState.Staging, (await store.FindAsync(manifest.Id, default))!.State);
        Assert.Empty((await scope.ServiceProvider.GetRequiredService<ICatalogReader>().SearchAsync("injected", CatalogKind.Artist, 10, 0, DateTimeOffset.UtcNow, default)).Items);
    }

    [LocalPostgresMigrationFact]
    public async Task CanonicalManualReplacementPreservesFeedbackExportsEvidenceAndRejectsStaleRevision()
    {
        await using var scope = _services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        Guid first = Guid.NewGuid(), second = Guid.NewGuid();
        await ImportAsync(services, [new(first, CatalogKind.Artist, "Same synthetic name", [], [], [new("jazz", 1)]),
            new(second, CatalogKind.Artist, "Same synthetic name", [], [], [new("ambient", 1)])]);
        var lifecycle = services.GetRequiredService<LinerNotes.Application.Features.Discovery.GuestLifecycle>();
        var profile = await lifecycle.StartAsync(new string('b', 64), default);
        var admission = services.GetRequiredService<IDiscoveryWriteCoordinator>();
        var db = services.GetRequiredService<DataContext>();
        var feedback = LinerNotes.Domain.Taste.TasteSignal.ForProfile(profile.Id, null, LinerNotes.Domain.Enums.TasteTargetType.Tag,
            "rock", 1, LinerNotes.Domain.Enums.TasteSignalSource.RecommendationLike, "Synthetic feedback evidence.");
        await admission.ExecuteAsync(8192, async ct => { db.TasteSignals.Add(feedback); await db.SaveChangesAsync(ct); return true; }, default);
        var items = (await services.GetRequiredService<ICatalogReader>().SearchAsync("same synthetic", CatalogKind.Artist, 10, 0, DateTimeOffset.UtcNow, default)).Items;
        var select = services.GetRequiredService<LinerNotes.Application.Features.Discovery.SelectDiscoveryTaste>();
        Assert.Equal(1, await select.ReplaceAsync(profile.Id, 0, new([items.Single(i => i.CanonicalId == first).Id], ["jazz"]), default));
        var export = await lifecycle.ExportAsync(profile.Id, default);
        var saved = Assert.Single(export.TasteSignals!, t => t.CanonicalEntityId == first);
        Assert.NotNull(saved.SelectionJson);
        Assert.Contains("generated-fixture", saved.SelectionJson);
        Assert.Equal(2, await select.ReplaceAsync(profile.Id, 1, new([items.Single(i => i.CanonicalId == second).Id], []), default));
        var current = await db.TasteSignals.AsNoTracking().Where(t => t.ProfileId == profile.Id).ToArrayAsync();
        Assert.Equal(2, current.Length);
        Assert.Contains(current, t => t.Id == feedback.Id);
        Assert.Contains(current, t => t.CanonicalEntityId == second);
        await Assert.ThrowsAsync<InvalidOperationException>(() => select.ReplaceAsync(profile.Id, 0, new([], ["pop"]), default));
        Assert.Equal(2, await db.TasteSignals.AsNoTracking().CountAsync(t => t.ProfileId == profile.Id));
        await lifecycle.ClearAsync(new string('b', 64), default);
        Assert.False(await db.TasteSignals.AsNoTracking().AnyAsync(t => t.ProfileId == profile.Id));
        Assert.Equal(2, await db.CatalogRecords.CountAsync());
    }

    [LocalPostgresMigrationFact]
    public async Task ExpiredCatalogCleanupIsBoundedAndPreservesTheHeadFreshLookupAndStaging()
    {
        await using var scope = _services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var now = DateTimeOffset.UtcNow;
        async Task Publish(int count, DateTimeOffset expires, bool lookup)
        {
            var rows = Enumerable.Range(0, count).Select(i => new CatalogArtifactRow(Guid.NewGuid(),
                CatalogKind.Artist, $"Synthetic cleanup artist {i}", [], [], [])).ToArray();
            using var stream = Artifact(rows);
            var publication = new CatalogPublication(Guid.NewGuid(), "generated-fixture", "cleanup-v1", "fixture:catalog",
                CatalogOrigin.Synthetic, "synthetic-fixture", null, "Synthetic mechanics only",
                Convert.ToHexStringLower(SHA256.HashData(stream.ToArray())), stream.Length, count, now, expires, lookup);
            await services.GetRequiredService<ImportCatalog>().RunAsync(publication, stream, false, false, default);
        }
        await Publish(3, now.AddDays(1), false);
        var db = services.GetRequiredService<DataContext>();
        var favoriteId = await db.CatalogRecords.OrderBy(r => r.Id).Select(r => r.Id).FirstAsync();
        var profile = await services.GetRequiredService<LinerNotes.Application.Features.Discovery.GuestLifecycle>()
            .StartAsync(new string('c', 64), default);
        await services.GetRequiredService<LinerNotes.Application.Features.Discovery.SelectDiscoveryTaste>()
            .ReplaceAsync(profile.Id, 0, new([favoriteId], []), default);
        string? savedSelection = (await db.TasteSignals.AsNoTracking().SingleAsync(t => t.ProfileId == profile.Id)).SelectionJson;
        Assert.NotNull(savedSelection);
        await Publish(1, now.AddDays(4), true);
        await Publish(1, now.AddDays(1), false);
        var staging = new CatalogPublication(Guid.NewGuid(), "generated-fixture", "staging-v1", "fixture:catalog",
            CatalogOrigin.Synthetic, "synthetic-fixture", null, "Synthetic mechanics only", new string('a', 64), 10, 1,
            now, now.AddDays(1));
        var admission = services.GetRequiredService<IDiscoveryWriteCoordinator>();
        await admission.ExecuteAsync(8192, async ct =>
        {
            await services.GetRequiredService<ICatalogImportStore>().StartAsync(staging, ct);
            return true;
        }, default);
        var maintenance = services.GetRequiredService<ICatalogMaintenance>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => maintenance.DeleteExpiredAsync(now.AddDays(2), 1, default));
        Assert.Equal(1, await admission.ReclaimAsync(ct => maintenance.DeleteExpiredAsync(now.AddDays(2), 1, ct), default));
        Assert.Equal(3, await admission.ReclaimAsync(ct => maintenance.DeleteExpiredAsync(now.AddDays(2), 100, ct), default));
        Assert.Equal(0, await admission.ReclaimAsync(ct => maintenance.DeleteExpiredAsync(now.AddDays(2), 100, ct), default));
        Assert.Equal(2, await db.CatalogRecords.CountAsync());
        Assert.Equal(3, await db.CatalogPublications.CountAsync());
        Assert.True(await db.CatalogPublications.AnyAsync(p => p.Id == staging.Id));
        Assert.Equal(5, await db.Artists.CountAsync());
        Assert.False(await db.CatalogRecords.AnyAsync(r => r.Id == favoriteId));
        var retained = await db.TasteSignals.AsNoTracking().SingleAsync(t => t.ProfileId == profile.Id);
        Assert.Equal(favoriteId, retained.CatalogRecordId);
        Assert.Equal(savedSelection, retained.SelectionJson);
    }

    private static MemoryStream Artifact(IReadOnlyList<CatalogArtifactRow> rows) => new(Encoding.UTF8.GetBytes(
        string.Join('\n', rows.Select(row => JsonSerializer.Serialize(row, CatalogArtifactReader.Json))) + "\n"));
    private static CatalogPublication Manifest(MemoryStream artifact, int count) => new(Guid.NewGuid(), "generated-fixture", "fixture-v1", "fixture:catalog",
        CatalogOrigin.Synthetic, "synthetic-fixture", "synthetic-fixture", "Synthetic mechanics only", Convert.ToHexStringLower(SHA256.HashData(artifact.ToArray())), artifact.Length, count, Acquired);
    private static async Task ImportAsync(IServiceProvider services, CatalogArtifactRow[] rows)
    {
        using var stream = Artifact(rows);
        await services.GetRequiredService<ImportCatalog>().RunAsync(Manifest(stream, rows.Length), stream, false, false, default);
    }
    private sealed class TransactionalAdmission(IUnitOfWork uow) : IDiscoveryWriteCoordinator
    {
        public Task<T> ReclaimAsync<T>(Func<CancellationToken, Task<T>> delete, CancellationToken ct) => ExecuteAsync(0, delete, ct);
        private int _writes;
        public int FailAfter { get; set; } = int.MaxValue;
        public void AllowNextWrites(int count) => FailAfter = _writes + count;
        public Task<T> ExecuteAsync<T>(long estimatedBytes, Func<CancellationToken, Task<T>> write, CancellationToken ct)
        {
            if (++_writes > FailAfter) throw new IOException("Injected pre-write interruption");
            return uow.ExecuteInTransactionAsync(write, ct);
        }
    }
    private sealed class DenyLookup : ICatalogLookupSource
    {
        public Task<CatalogLookupResult> SearchAsync(string query, CatalogKind kind, CancellationToken ct) => throw new InvalidOperationException("Autocomplete must never contact a provider.");
    }
}
