using LinerNotes.Application;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.DataAccess.Persistence;
using LinerNotes.Domain.Discovery;
using LinerNotes.Infrastructure;
using LinerNotes.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
namespace LinerNotes.DataAccess.Tests;

public sealed class DiscoveryCoordinationPostgresTests : IAsyncLifetime
{
    private readonly string name = "liner_coord_c7_" + Guid.NewGuid().ToString("N");
    private readonly string root = Path.Combine(Path.GetTempPath(), "liner-coord-c7-" + Guid.NewGuid().ToString("N"));
    private string admin = "", connection = "";
    private ServiceProvider services = null!;
    private IServiceCollection registrations = null!;
    public async Task InitializeAsync()
    {
        admin = Environment.GetEnvironmentVariable("LINER_PHASE1_POSTGRES") ?? "";
        if (admin.Length == 0) return;
        var settings = new NpgsqlConnectionStringBuilder(admin);
        Assert.Contains(settings.Host, new[] { "localhost", "127.0.0.1", "::1" });
        await using (var db = new NpgsqlConnection(admin))
        {
            await db.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", db); await create.ExecuteNonQueryAsync();
        }
        settings.Database = name; settings.Pooling = false; connection = settings.ConnectionString;
        Directory.CreateDirectory(root);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:DefaultConnection"] = connection, ["Generation:Origin"] = "Synthetic", ["LastFm:Mode"] = "FixtureOnly"
        }).Build();
        registrations = new ServiceCollection().AddLogging().AddApplication().AddDataAccess(config).AddInfrastructure(config);
        registrations.AddSingleton(new GenerationStorageOptions {
            InventoryPath = Path.Combine(root, "inventory.json"), LeasePath = Path.Combine(root, "lease"), DatabaseOverheadBytesPerPick = 1_048_576,
            ArtifactRoots = LocalGenerationStorage.Categories.ToDictionary(k => k, _ => Array.Empty<string>()) });
        registrations.AddSingleton(new CoordinatedStorageOptions { FilesystemPaths = [root], MinimumFreeBytes = 67_108_864 });
        // Crash checks must observe server rollback, not an automatic replay of the test fault.
        registrations.AddDbContext<DataContext>(b => b.UseNpgsql(connection, n => n.ExecutionStrategy(d => new NonRetryingExecutionStrategy(d))));
        services = registrations.BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        await ReconcileAsync();
    }
    public async Task DisposeAsync()
    {
        if (services is not null) await services.DisposeAsync();
        if (admin.Length > 0)
        {
            await using var db = new NpgsqlConnection(admin); await db.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", db); await drop.ExecuteNonQueryAsync();
        }
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
    private async Task ReconcileAsync()
    {
        await using var scope = services.CreateAsyncScope();
        await using var batch = await scope.ServiceProvider.GetRequiredService<IDigestGenerationStore>().AcquireBatchAsync();
        await scope.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
        var state = await scope.ServiceProvider.GetRequiredService<IDigestGenerationStore>().ReadStorageAsync();
        await scope.ServiceProvider.GetRequiredService<LocalGenerationStorage>().ReconcileAsync(state);
    }
    private async Task<ListenerProfile> GuestAsync(char hash)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<GuestLifecycle>().StartAsync(new string(hash, 64), default);
    }
    private async Task<T> WriteAsync<T>(Func<IServiceProvider, CancellationToken, Task<T>> operation, long estimate = 65_536)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IDiscoveryWriteCoordinator>().ExecuteAsync(estimate, ct => operation(scope.ServiceProvider, ct), default);
    }
    [LocalPostgresMigrationFact]
    public async Task EnqueueReplayConflictCooldownOwnershipAndLeaseFencingSurviveFreshScopes()
    {
        var profile = await GuestAsync('c');
        var other = await GuestAsync('d');
        var job = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(profile.Id, "first", new string('a', 64),
            DiscoveryJobKind.Generation, 0, "{\"initial\":true}", DateTimeOffset.UtcNow, 32, TimeSpan.FromSeconds(30), ct));
        var replay = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(profile.Id, "first", new string('a', 64),
            DiscoveryJobKind.Generation, 0, "{}", DateTimeOffset.UtcNow, 32, TimeSpan.FromSeconds(30), ct));
        Assert.Equal(job.Id, replay.Id);
        await Assert.ThrowsAsync<DiscoveryRequestConflictException>(() => WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(profile.Id,
            "first", new string('b', 64), DiscoveryJobKind.Generation, 0, "{}", DateTimeOffset.UtcNow, 32, TimeSpan.Zero, ct)));
        var first = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().LeaseNextAsync(DiscoveryJobKind.Generation, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5), 3, ct));
        Assert.NotNull(first);
        var reclaimedAt = first!.LeaseExpiresAtUtc!.Value;
        var second = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().LeaseNextAsync(DiscoveryJobKind.Generation, reclaimedAt, TimeSpan.FromSeconds(5), 3, ct));
        Assert.NotEqual(first.LeaseToken, second!.LeaseToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => WriteAsync(async (s, ct) => {
            await s.GetRequiredService<IDiscoveryJobStore>().RenewAsync(job.Id, first.LeaseToken!.Value, reclaimedAt.AddSeconds(1), TimeSpan.FromSeconds(5), "scoring", ct); return true; }));
        await WriteAsync(async (s, ct) => { await s.GetRequiredService<IDiscoveryJobStore>().FailAsync(job.Id, second.LeaseToken!.Value,
            reclaimedAt.AddSeconds(1), "unsupported_source", false, TimeSpan.Zero, 3, ct); return true; });
        await Assert.ThrowsAsync<DiscoveryBusyException>(() => WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(profile.Id,
            "more", new string('a', 64), DiscoveryJobKind.Generation, 0, "{}", job.CreatedAtUtc.AddSeconds(2), 32, TimeSpan.FromSeconds(30), ct)));
        await using var read = services.CreateAsyncScope();
        var store = read.ServiceProvider.GetRequiredService<IDiscoveryJobStore>();
        Assert.Null(await store.FindAsync(other.Id, job.Id, DateTimeOffset.UtcNow, default));
        var saved = await store.FindAsync(profile.Id, job.Id, DateTimeOffset.UtcNow, default);
        Assert.Equal(DiscoveryJobState.Failed, saved!.State); Assert.Equal("{}", saved.PayloadJson);
    }
    [LocalPostgresMigrationFact]
    public async Task CompetingSubmissionsProduceOnePendingJobAndFailedInputJobActionRollsBack()
    {
        var profile = await GuestAsync('e');
        async Task<bool> Submit(string key)
        {
            try { await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(profile.Id, key, new string('a', 64),
                DiscoveryJobKind.Generation, 0, "{}", DateTimeOffset.UtcNow, 32, TimeSpan.Zero, ct)); return true; }
            catch (DiscoveryBusyException) { return false; }
            catch (GenerationStoppedException ex) when (ex.Message == "storage_lease_unavailable") { return false; }
        }
        var results = await Task.WhenAll(Submit("one"), Submit("two"));
        Assert.Single(results, result => result);
        var another = await GuestAsync('f');
        await Assert.ThrowsAsync<IOException>(() => WriteAsync(async (s, ct) => {
            await s.GetRequiredService<SelectDiscoveryTaste>().ReplaceAsync(another.Id, 0, new([], ["jazz"]), ct);
            await s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(another.Id, "atomic", new string('a', 64), DiscoveryJobKind.Generation,
                1, "{}", DateTimeOffset.UtcNow, 32, TimeSpan.Zero, ct);
            throw new IOException("Injected failure before publication");
#pragma warning disable CS0162
            return true;
#pragma warning restore CS0162
        }, 262_144));
        await using var read = services.CreateAsyncScope(); var db = read.ServiceProvider.GetRequiredService<DataContext>();
        Assert.False(await db.TasteSignals.AnyAsync(s => s.ProfileId == another.Id));
        Assert.False(await db.DiscoveryJobs.AnyAsync(j => j.ProfileId == another.Id));
        Assert.Equal(0, (await db.ListenerProfiles.SingleAsync(p => p.Id == another.Id)).Revision);
    }
    [LocalPostgresMigrationFact]
    public async Task UntrackedSchemaAndTrackedExternalWritesStopAdmissionUntilReconciled()
    {
        var profile = await GuestAsync('a');
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            db.TasteSignals.Add(LinerNotes.Domain.Taste.TasteSignal.ForProfile(profile.Id, null, LinerNotes.Domain.Enums.TasteTargetType.Tag,
                "jazz", 1, LinerNotes.Domain.Enums.TasteSignalSource.InitialSeedManual, "Synthetic external writer."));
            await db.SaveChangesAsync();
        }
        var stale = await Assert.ThrowsAsync<GenerationStoppedException>(() => GuestAsync('b'));
        Assert.Equal("database_inventory_changed", stale.Message);
        await ReconcileAsync(); await GuestAsync('b');
        await using var schema = new NpgsqlConnection(connection); await schema.OpenAsync();
        await using (var create = new NpgsqlCommand("CREATE TABLE public.c7_untracked (id integer)", schema)) await create.ExecuteNonQueryAsync();
        var unknown = await Assert.ThrowsAsync<GenerationStoppedException>(() => GuestAsync('7'));
        Assert.Equal("managed_storage_ledger_missing_or_schema_uncovered", unknown.Message);
        await using var drop = new NpgsqlCommand("DROP TABLE public.c7_untracked", schema); await drop.ExecuteNonQueryAsync();
    }
    [LocalPostgresMigrationFact]
    public async Task CapacityRaceAdmitsOneMutationAndOversizedPublicationRollsBack()
    {
        var first = await GuestAsync('1'); var second = await GuestAsync('2');
        long physical;
        await using (var scope = services.CreateAsyncScope()) physical = (await scope.ServiceProvider.GetRequiredService<IDigestGenerationStore>().ReadStorageAsync()).DatabaseBytes;
        long offset = LocalGenerationStorage.StopBytes - physical - 1_350_000;
        // Controlled occupancy injection; this does not fill the developer's actual filesystem.
        await services.DisposeAsync();
        registrations.AddScoped<LinerNotes.DataAccess.Persistence.Repositories.DigestGenerationStore>();
        registrations.AddScoped<IDigestGenerationStore>(s => new OffsetInventory(s.GetRequiredService<LinerNotes.DataAccess.Persistence.Repositories.DigestGenerationStore>(), offset));
        services = registrations.BuildServiceProvider(); await ReconcileAsync();
        async Task<bool> Publish(Guid profile)
        {
            try { return await WriteAsync((s, ct) => AddPressureRowsAsync(s, profile, 24, ct), estimate: 0); }
            catch (GenerationStoppedException ex) when (ex.Message is "storage_headroom" or "storage_lease_unavailable") { return false; }
        }
        var accepted = await Task.WhenAll(Publish(first.Id), Publish(second.Id));
        Assert.Single(accepted, a => a);
        await using var check = services.CreateAsyncScope();
        Assert.Equal(24, await check.ServiceProvider.GetRequiredService<DataContext>().DiscoveryJobs.CountAsync());
    }
    [LocalPostgresMigrationFact]
    public async Task ActualPayloadExceedingReservationRollsBackJobsAndAccounting()
    {
        var profile = await GuestAsync('3');
        var failure = await Assert.ThrowsAsync<GenerationStoppedException>(() => WriteAsync(async (s, ct) => {
            var context = s.GetRequiredService<DataContext>();
            var changing = await context.ListenerProfiles.AsTracking().SingleAsync(p => p.Id == profile.Id, ct);
            changing.ChangeInputs(0);
            context.TasteSignals.Add(LinerNotes.Domain.Taste.TasteSignal.ForProfile(profile.Id, null, LinerNotes.Domain.Enums.TasteTargetType.Tag,
                "jazz", 1, LinerNotes.Domain.Enums.TasteSignalSource.InitialSeedManual, "Synthetic accepted input in rejected publication."));
            return await AddPressureRowsAsync(s, profile.Id, 80, ct);
        }, estimate: 0));
        Assert.Equal("stored_bytes_exceed_reservation", failure.Message);
        await using (var read = services.CreateAsyncScope())
        {
            var context = read.ServiceProvider.GetRequiredService<DataContext>();
            Assert.Equal(0, await context.DiscoveryJobs.CountAsync());
            Assert.Equal(0, await context.TasteSignals.CountAsync());
            Assert.Equal(0, (await context.ListenerProfiles.SingleAsync(p => p.Id == profile.Id)).Revision);
        }
        await GuestAsync('4'); // Physical rollback growth is measured, never silently deducted from usage.
    }
    [LocalPostgresMigrationFact]
    public async Task DeletionRevokesAnInflightLeaseWithoutOrphanResults()
    {
        var profile = await GuestAsync('5');
        await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(profile.Id, "delete", new string('a', 64),
            DiscoveryJobKind.Generation, 0, "{}", DateTimeOffset.UtcNow, 32, TimeSpan.Zero, ct));
        var leased = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().LeaseNextAsync(DiscoveryJobKind.Generation, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(30), 3, ct));
        await using (var deleting = services.CreateAsyncScope()) await deleting.ServiceProvider.GetRequiredService<GuestLifecycle>().ClearAsync(new string('5', 64), default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => WriteAsync(async (s, ct) => {
            await s.GetRequiredService<IDiscoveryJobStore>().SaveCompletedAsync(leased!, leased!.LeaseToken!.Value, DateTimeOffset.UtcNow, Guid.NewGuid(), null, ct); return true; }));
        await using var read = services.CreateAsyncScope(); var db = read.ServiceProvider.GetRequiredService<DataContext>();
        Assert.Equal(0, await db.DiscoveryJobs.CountAsync()); Assert.Equal(0, await db.DiscoveryBatches.CountAsync());
    }
    private static async Task<bool> AddPressureRowsAsync(IServiceProvider services, Guid profile, int count, CancellationToken ct)
    {
        var db = services.GetRequiredService<DataContext>();
        for (int i = 0; i < count; i++)
        {
            var now = DateTimeOffset.UtcNow;
            var job = new DiscoveryJob(profile, "pressure-" + i, new string('a', 64), DiscoveryJobKind.Lookup, 0, "{}", now);
            var token = Guid.NewGuid(); job.AcquireLease(token, now, TimeSpan.FromSeconds(30), 3);
            string random = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16_000));
            job.Complete(token, now, null, "{\"synthetic\":\"" + random + "\"}"); db.DiscoveryJobs.Add(job);
        }
        await db.SaveChangesAsync(ct); return true;
    }
    private sealed class OffsetInventory(IDigestGenerationStore inner, long offset) : IDigestGenerationStore
    {
        public Task<IAsyncDisposable> AcquireBatchAsync(CancellationToken ct = default) => inner.AcquireBatchAsync(ct);
        public Task<LinerNotes.Domain.Digest.WeeklyDigest?> GetExistingAsync(Guid user, LinerNotes.Domain.Digest.IsoWeek week, CancellationToken ct = default) => inner.GetExistingAsync(user, week, ct);
        public Task<GenerationInputs> ReadInputsAsync(Guid user, CancellationToken ct = default) => inner.ReadInputsAsync(user, ct);
        public async Task<GenerationStorageState> ReadStorageAsync(CancellationToken ct = default)
        { var state = await inner.ReadStorageAsync(ct); return state with { DatabaseBytes = checked(state.DatabaseBytes + offset) }; }
        public Task<LinerNotes.Domain.Digest.WeeklyDigest> PersistAsync(Guid user, LinerNotes.Domain.Digest.IsoWeek week, string revision,
            IReadOnlyList<LinerNotes.Domain.Scoring.BaselineAPick> picks, IGenerationStorageReservation reservation, CancellationToken ct = default)
            => inner.PersistAsync(user, week, revision, picks, reservation, ct);
    }

    [LocalPostgresMigrationFact]
    public async Task ExplicitReconciliationDetectsCounterCorruptionWithoutRepairingIt()
    {
        var profile = await GuestAsync('9');
        await WriteAsync(async (s, ct) => {
            var context = s.GetRequiredService<DataContext>();
            context.TasteSignals.Add(LinerNotes.Domain.Taste.TasteSignal.ForProfile(profile.Id, null, LinerNotes.Domain.Enums.TasteTargetType.Tag,
                "jazz", 1, LinerNotes.Domain.Enums.TasteSignalSource.InitialSeedManual, "Synthetic truncate accounting probe."));
            await context.SaveChangesAsync(ct); return true;
        });
        await using (var truncating = new NpgsqlConnection(connection))
        {
            await truncating.OpenAsync();
            await using var truncate = new NpgsqlCommand("TRUNCATE \"TasteSignals\"", truncating); await truncate.ExecuteNonQueryAsync();
        }
        await ReconcileAsync(); // Statement-level accounting also covers an explicit operator TRUNCATE.
        await using (var db = new NpgsqlConnection(connection))
        {
            await db.OpenAsync();
            await using var corrupt = new NpgsqlCommand("UPDATE \"ManagedStorageLedger\" SET \"RowCount\"=\"RowCount\"+1 WHERE \"Id\"=1", db);
            await corrupt.ExecuteNonQueryAsync();
        }
        var failure = await Assert.ThrowsAsync<GenerationStoppedException>(ReconcileAsync);
        Assert.Equal("managed_storage_ledger_mismatch", failure.Message);
    }

    [LocalPostgresMigrationFact]
    public async Task PublicEvidenceLeasesCoalesceFenceAndPreserveExactChecksummedBytes()
    {
        string key = new string('a', 64); var now = DateTimeOffset.UtcNow;
        now = now.AddTicks(-(now.Ticks % 10)); // PostgreSQL timestamps retain microseconds.
        var first = await WriteAsync((s, ct) => s.GetRequiredService<IPublicEvidenceStore>().ClaimAsync(key, PublicEvidenceKind.Pool, now, TimeSpan.FromSeconds(15), ct));
        Assert.NotNull(first);
        Assert.Null(await WriteAsync((s, ct) => s.GetRequiredService<IPublicEvidenceStore>().ClaimAsync(key, PublicEvidenceKind.Pool, now, TimeSpan.FromSeconds(15), ct)));
        var reclaimedAt = first!.ExpiresAtUtc;
        var second = await WriteAsync((s, ct) => s.GetRequiredService<IPublicEvidenceStore>().ClaimAsync(key, PublicEvidenceKind.Pool, reclaimedAt, TimeSpan.FromSeconds(15), ct));
        var document = LinerNotes.Infrastructure.Discovery.PublicEvidenceCodec.Document(key, PublicEvidenceKind.Pool, "listenbrainz",
            LinerNotes.Application.Common.Models.Recommendation.EvidenceOrigin.Synthetic, "fixture-v1", new { Facts = new[] { "Synthetic music" } }, now, now.AddMinutes(10));
        await Assert.ThrowsAsync<InvalidOperationException>(() => WriteAsync(async (s, ct) => {
            await s.GetRequiredService<IPublicEvidenceStore>().PublishAsync(first, document, reclaimedAt, 1_048_576, 10, ct); return true; }));
        await WriteAsync(async (s, ct) => { await s.GetRequiredService<IPublicEvidenceStore>().PublishAsync(second!, document, reclaimedAt,
            1_048_576, 10, ct); return true; });
        await using var read = services.CreateAsyncScope();
        var saved = await read.ServiceProvider.GetRequiredService<IPublicEvidenceStore>().FindAsync(key, reclaimedAt, default);
        Assert.Equal(document, saved);
        Assert.Null(await read.ServiceProvider.GetRequiredService<IPublicEvidenceStore>().FindAsync(key, document.ExpiresAtUtc, default));
    }

    [LocalPostgresMigrationFact]
    public async Task KillingBackendBeforeCommitRollsBackRowsLedgerAndAllowsFreshAdmission()
    {
        var entered = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = WriteAsync(async (s, ct) => {
            var db = s.GetRequiredService<DataContext>();
            var profile = ListenerProfile.Guest(DateTimeOffset.UtcNow, TimeSpan.FromDays(7)); db.ListenerProfiles.Add(profile); await db.SaveChangesAsync(ct);
            var pg = (NpgsqlConnection)db.Database.GetDbConnection(); entered.SetResult(pg.ProcessID);
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10)); return profile.Id;
        });
        int pid = await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await using var control = new NpgsqlConnection(admin); await control.OpenAsync();
        await using (var kill = new NpgsqlCommand("SELECT pg_terminate_backend(@pid)", control)) { kill.Parameters.AddWithValue("pid", pid); Assert.True((bool)(await kill.ExecuteScalarAsync())!); }
        bool gone = false;
        for (int i = 0; i < 100; i++)
        {
            await using var check = new NpgsqlCommand("SELECT NOT EXISTS (SELECT 1 FROM pg_stat_activity WHERE pid=@pid)", control); check.Parameters.AddWithValue("pid", pid);
            if ((bool)(await check.ExecuteScalarAsync())!) { gone = true; break; }
            await Task.Delay(20);
        }
        Assert.True(gone, "Server rollback must finish before recovery assertions."); release.SetResult();
        await Assert.ThrowsAnyAsync<Exception>(async () => await pending);
        await using (var scope = services.CreateAsyncScope()) Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<DataContext>().ListenerProfiles.CountAsync());
        await GuestAsync('8');
    }
}
