using System.Diagnostics;
using System.Text.Json;
using LinerNotes.Application;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Features.Digests.Commands.RunScheduledTick;
using LinerNotes.Application.Features.Digests.Commands.GenerateDigest;
using LinerNotes.Application.Features.Export.Queries.GetUserDataExport;
using LinerNotes.Application.Features.Subscribers.Commands.DeleteUserAccount;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.DataAccess.IdentityEntities;
using LinerNotes.DataAccess.Persistence;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Taste;
using LinerNotes.Infrastructure;
using LinerNotes.Infrastructure.Email;
using LinerNotes.Infrastructure.Storage;
using LinerNotes.Worker;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed class Phase8ScheduledWorkerPostgresTests : IAsyncLifetime
{
    private readonly string _name = "liner_phase8_schedule_" + Guid.NewGuid().ToString("N");
    private readonly string _root = Path.Combine(Path.GetTempPath(), "liner-phase8-schedule-" + Guid.NewGuid().ToString("N"));
    private readonly Guid _user = Guid.NewGuid(), _other = Guid.NewGuid();
    private readonly DateTime _due = CurrentHour();
    private string _admin = "", _connection = "";
    private ServiceProvider _services = null!;
    private IConfiguration _config = null!;
    private GenerationStorageOptions _storage = null!;
    private LocalEmailOptions _email = null!;
    private Clock _clock = null!;

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
        settings.Database = _name; settings.Pooling = false; _connection = settings.ConnectionString;
        Directory.CreateDirectory(_root);
        _storage = new()
        {
            InventoryPath = Path.Combine(_root, "inventory.json"), LeasePath = Path.Combine(_root, "lease"),
            DatabaseOverheadBytesPerPick = 1_000_000,
            ArtifactRoots = LocalGenerationStorage.Categories.ToDictionary(category => category, category => new[] { Path.Combine(_root, category) })
        };
        foreach (var path in _storage.ArtifactRoots.Values.SelectMany(paths => paths)) Directory.CreateDirectory(path);
        _email = new() { Enabled = true, ApplicationOrigin = "http://127.0.0.1:5000", SinkRoot = Path.Combine(_root, "copies") };
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(_email.SinkRoot, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        _clock = new() { Now = new(_due.AddMinutes(30)) };
        _config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:DefaultConnection"] = _connection, ["Generation:Origin"] = "Synthetic", ["LastFm:Mode"] = "FixtureOnly" }).Build();
        var registrations = new ServiceCollection().AddLogging().AddApplication().AddDataAccess(_config).AddInfrastructure(_config);
        registrations.AddSingleton(_storage).AddSingleton(_email).AddSingleton<TimeProvider>(_clock);
        _services = registrations.BuildServiceProvider();
        await using var scope = _services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        foreach (var id in new[] { _user, _other })
        {
            db.ApplicationUsers.Add(new ApplicationUser { Id = id, UserName = "fixture-" + id.ToString("N"), Email = "schedule@example.test" });
            var user = new User(id == _user ? "schedule@example.test" : "other@example.test", "Asia/Baku", (DigestDeliveryDay)_due.DayOfWeek, _due.Hour, id);
            user.SetNextDigestAt(_due); db.Users.Add(user);
            db.TasteSignals.Add(TasteSignal.CreateSeedTag(id, "darkwave", 1, "synthetic schedule fixture"));
            db.UserTokens.Add(new() { UserId = id, LoginProvider = "fixture", Name = "fixture", Value = "fixture-only-not-a-real-credential" });
        }
        await db.SaveChangesAsync();
        _ = scope.ServiceProvider.GetRequiredService<IUnsubscribeTokens>().Create(_user);
        await ReconcileAsync();
    }

    private async Task ReconcileAsync()
    {
        await using var scope = _services.CreateAsyncScope();
        var state = await scope.ServiceProvider.GetRequiredService<IDigestGenerationStore>().ReadStorageAsync();
        await scope.ServiceProvider.GetRequiredService<LocalGenerationStorage>().ReconcileAsync(state);
    }

    private async Task<ScheduleTickResult> TickAsync()
    {
        await using var scope = _services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunScheduledTickCommand([_user], 1));
        if (result.Status == "stopped") Console.WriteLine(JsonSerializer.Serialize(result));
        return result;
    }
    [LocalPostgresMigrationFact]
    public async Task PersistentInvalidAccount_DoesNotStarveHealthyAccount_AcrossStoreScopes()
    {
        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            await db.Users.Where(u => u.Id == _user).ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.NextDigestAt, _due.AddDays(-1))
                .SetProperty(u => u.LastModifiedAt, _due.AddDays(-2)));
        }
        async Task<ScheduleTickResult> Run()
        {
            await using var scope = _services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunScheduledTickCommand([_user, _other], 1));
        }
        var first = Assert.Single((await Run()).Jobs);
        Assert.Equal(_user, first.UserId); Assert.Equal("invalid_schedule_state", first.Reason);
        var second = Assert.Single((await Run()).Jobs);
        Assert.Equal(_other, second.UserId);
        if (second.Status == "stopped")
        {
            Assert.Equal("database_inventory_changed", second.Reason);
            await ReconcileAsync();
        }
        var third = Assert.Single((await Run()).Jobs);
        Assert.Equal(_user, third.UserId); Assert.Equal("invalid_schedule_state", third.Reason);
        for (var attempt = 0; second.Status != "advanced" && attempt < 3; attempt++)
        {
            second = Assert.Single((await Run()).Jobs);
            Assert.Equal(_other, second.UserId);
            if (second.Status == "advanced") break;
            Assert.Equal("database_inventory_changed", second.Reason);
            await ReconcileAsync();
            Assert.Equal(_user, Assert.Single((await Run()).Jobs).UserId);
        }
        Assert.Equal("advanced", second.Status);
    }

    [LocalPostgresMigrationFact]
    public async Task CandidateAttempts_RotateEvenWithUnchangedDueSlotsAndClock()
    {
        var seen = new List<Guid>();
        for (var i = 0; i < 4; i++)
        {
            await using var scope = _services.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IWeeklyScheduleStore>();
            await using var lease = await store.TryAcquireTickAsync(default);
            Assert.NotNull(lease);
            seen.Add(Assert.Single(await store.GetCandidatesAsync([_user, _other], 1, _clock.Now.UtcDateTime, default)).Id);
        }
        Assert.NotEqual(seen[0], seen[1]); Assert.Equal(seen[0], seen[2]); Assert.Equal(seen[1], seen[3]);
    }

    [LocalPostgresMigrationFact]
    public async Task RegistrationRepair_RequiresLegacyPatternExpectedSlotAndNoDigest()
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var store = (LinerNotes.DataAccess.Persistence.Repositories.WeeklyScheduleStore)scope.ServiceProvider.GetRequiredService<IWeeklyScheduleStore>();
        var legacy = _due.AddDays(-1);
        await db.Users.Where(u => u.Id == _user).ExecuteUpdateAsync(setters => setters
            .SetProperty(u => u.CreatedAt, legacy.AddDays(-7).AddMinutes(1))
            .SetProperty(u => u.NextDigestAt, legacy));
        var observed = (await store.GetAsync(_user, default))!;
        Assert.False(await store.RepairRegistrationSlotAsync(observed with { NextDigestAt = legacy.AddDays(-7) }, _due, _due, default));
        Assert.True(await store.RepairRegistrationSlotAsync(observed, _due, _due, default));
        Assert.False(await store.RepairRegistrationSlotAsync(observed, _due, _due, default));
        Assert.False(await store.RepairRegistrationSlotAsync((await store.GetAsync(_user, default))!, _due.AddDays(7), _due, default));
        await db.Users.Where(u => u.Id == _user).ExecuteUpdateAsync(setters => setters.SetProperty(u => u.NextDigestAt, legacy.AddDays(-7)));
        Assert.False(await store.RepairRegistrationSlotAsync((await store.GetAsync(_user, default))!, _due, _due, default));
        await db.Users.Where(u => u.Id == _user).ExecuteUpdateAsync(setters => setters.SetProperty(u => u.NextDigestAt, legacy));
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GenerateDigestCommand(_user, WeeklySchedulePolicy.Week(legacy)));
        Assert.True(await db.WeeklyDigests.AnyAsync(d => d.UserId == _user));
        Assert.False(await store.RepairRegistrationSlotAsync((await store.GetAsync(_user, default))!, _due, _due, default));
    }

    private async Task PrepareCaptureAsync()
    {
        await using var scope = _services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GenerateDigestCommand(_user, WeeklySchedulePolicy.Week(_due)));
        Assert.Equal("generated", result.Status);
        var store = scope.ServiceProvider.GetRequiredService<IWeeklyScheduleStore>();
        await store.GetCandidatesAsync([_user], 1, _clock.Now.UtcDateTime, default);
        await store.GetAsync(_user, default);
        await using (var lease = await store.TryAcquireTickAsync(default))
            await lease!.EnsureHeldAsync(default);
        await using (var rows = await scope.ServiceProvider.GetRequiredService<ILocalDigestCaptureStore>()
            .BeginAsync(_user, WeeklySchedulePolicy.Week(_due), default)) { }
        await ReconcileAsync();
    }
    private async Task CaptureAfterExplicitReconciliationAsync()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await ReconcileAsync();
            var result = await TickAsync();
            if (Assert.Single(result.Jobs).Status == "advanced") return;
            // PostgreSQL can allocate physical pages on first access. Each guarded stop
            // requires another explicit operator measurement; never bypass the guard.
            Assert.Equal("database_inventory_changed", Assert.Single(result.Jobs).Reason);
            await using var scope = _services.CreateAsyncScope();
            Assert.Equal(_due, (await scope.ServiceProvider.GetRequiredService<DataContext>().Users.SingleAsync(u => u.Id == _user)).NextDigestAt);
            Assert.Empty(Directory.GetFiles(_email.SinkRoot!));
        }
        Assert.Fail("Capture did not complete after three explicit operator reconciliations.");
    }

    [LocalPostgresMigrationFact]
    public async Task RealTick_ConcurrentAndRepeated_ProducesOneReceiptAndAdvancesOneSlot()
    {
        await PrepareCaptureAsync();
        ScheduleTickResult[] results = [];
        for (var attempt = 0; attempt < 3; attempt++)
        {
            results = await Task.WhenAll(TickAsync(), TickAsync());
            if (results.SelectMany(result => result.Jobs).Any(job => job.Status == "advanced")) break;
            Assert.All(results.SelectMany(result => result.Jobs), job => Assert.Equal("database_inventory_changed", job.Reason));
            Assert.Empty(Directory.GetFiles(_email.SinkRoot!));
            await using var unchanged = _services.CreateAsyncScope();
            Assert.Equal(_due, (await unchanged.ServiceProvider.GetRequiredService<DataContext>().Users.SingleAsync(u => u.Id == _user)).NextDigestAt);
            await ReconcileAsync(); // An explicit operator retry, before any successful capture/advance.
        }
        Assert.All(results, result => Assert.Contains(result.Status, new[] { "completed", "scheduler_busy" }));
        Assert.Equal(1, results.SelectMany(result => result.Jobs).Count(job => job.Status == "advanced"));
        Assert.Empty((await TickAsync()).Jobs);
        await using var scope = _services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        Assert.Single(await db.WeeklyDigests.ToListAsync()); Assert.Single(Directory.GetFiles(_email.SinkRoot!, "*.eml.enc"));
        Assert.Equal(_due.AddDays(7), (await db.Users.SingleAsync(user => user.Id == _user)).NextDigestAt);
        Assert.Equal(_due, (await db.Users.SingleAsync(user => user.Id == _other)).NextDigestAt);
        Assert.Equal(DigestStatus.LocalCaptured, (await db.WeeklyDigests.SingleAsync()).Status);
        Assert.Null((await db.WeeklyDigests.SingleAsync()).SentAt);
    }

    [LocalPostgresMigrationFact]
    public async Task Store_LeaseContentionReleaseAndStaleAdvance_AreGuarded()
    {
        await using var scope = _services.CreateAsyncScope(); var store = scope.ServiceProvider.GetRequiredService<IWeeklyScheduleStore>();
        await using (var lease = await store.TryAcquireTickAsync(default))
        {
            Assert.NotNull(lease); await lease.EnsureHeldAsync(default);
            Assert.Null(await store.TryAcquireTickAsync(default));
            Assert.Equal("scheduler_busy", (await TickAsync()).Status);
        }
        await using var reacquired = await store.TryAcquireTickAsync(default); Assert.NotNull(reacquired);
        var observed = (await store.GetAsync(_user, default))!;
        Assert.True(await store.AdvanceAsync(observed, _due.AddDays(7), _clock.Now.UtcDateTime, default));
        Assert.False(await store.AdvanceAsync(observed, _due.AddDays(14), _clock.Now.UtcDateTime, default));
        await scope.ServiceProvider.GetRequiredService<IUnsubscribeStore>().UnsubscribeAsync(_user, _clock.Now.UtcDateTime, default);
        Assert.Null(await store.GetAsync(_user, default));
        Assert.False(await store.AdvanceAsync(observed with { NextDigestAt = _due.AddDays(7) }, _due.AddDays(14), _clock.Now.UtcDateTime, default));
    }

    [LocalPostgresMigrationFact]
    public async Task NullSchedule_InitializesOnce_AndFiltersHiddenIdentityAndUnsubscribedAccounts()
    {
        await using var scope = _services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        await db.Users.Where(user => user.Id == _user).ExecuteUpdateAsync(setters => setters.SetProperty(user => user.NextDigestAt, (DateTime?)null));
        var store = scope.ServiceProvider.GetRequiredService<IWeeklyScheduleStore>();
        var original = (await store.GetAsync(_user, default))!;
        var future = _due.AddDays(7);
        Assert.True(await store.InitializeAsync(original, future, _clock.Now.UtcDateTime, default));
        Assert.False(await store.InitializeAsync(original, future.AddDays(7), _clock.Now.UtcDateTime, default));
        Assert.Empty(await store.GetCandidatesAsync([_user], 1, _clock.Now.UtcDateTime, default));
        await db.ApplicationUsers.Where(user => user.Id == _other).ExecuteUpdateAsync(setters => setters.SetProperty(user => user.IsDeleted, true));
        Assert.Empty(await store.GetCandidatesAsync([_other], 1, _clock.Now.UtcDateTime, default));
        Assert.Null(await store.GetAsync(_other, default));
    }

    [LocalPostgresMigrationFact]
    public async Task ChangedPreferencesAndDeletedAccount_RejectStaleAdvance()
    {
        await using var scope = _services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var store = scope.ServiceProvider.GetRequiredService<IWeeklyScheduleStore>();
        var observed = (await store.GetAsync(_user, default))!;
        await db.Users.Where(user => user.Id == _user).ExecuteUpdateAsync(setters => setters.SetProperty(user => user.DeliveryHourUtc, (_due.Hour + 1) % 24));
        Assert.False(await store.AdvanceAsync(observed, _due.AddDays(7), _clock.Now.UtcDateTime, default));
        Assert.Equal("invalid_schedule_state", Assert.Single((await TickAsync()).Jobs).Reason);
        Assert.Equal(_due, (await db.Users.SingleAsync(user => user.Id == _user)).NextDigestAt);
        await db.ApplicationUsers.Where(user => user.Id == _user).ExecuteDeleteAsync();
        Assert.Null(await store.GetAsync(_user, default));
        Assert.False(await store.AdvanceAsync(observed, _due.AddDays(7), _clock.Now.UtcDateTime, default));
        Assert.False(await db.Users.IgnoreQueryFilters().AnyAsync(user => user.Id == _user));
        Assert.Empty(Directory.GetFiles(_email.SinkRoot!));
    }

    [LocalPostgresMigrationFact]
    public async Task MissingCompletedReceipt_StopsTheDueSlotWithoutRecreating()
    {
        await PrepareCaptureAsync();
        await CaptureAfterExplicitReconciliationAsync();
        await using var scope = _services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        await db.Users.Where(user => user.Id == _user).ExecuteUpdateAsync(setters => setters.SetProperty(user => user.NextDigestAt, _due));
        File.Delete(Assert.Single(Directory.GetFiles(_email.SinkRoot!, "*.eml.enc")));
        Assert.Equal("email_receipt_missing", Assert.Single((await TickAsync()).Jobs).Reason);
        Assert.Equal(_due, (await db.Users.SingleAsync(user => user.Id == _user)).NextDigestAt);
        Assert.Empty(Directory.GetFiles(_email.SinkRoot!)); Assert.Single(await db.WeeklyDigests.ToListAsync());
        // Make the healthy account a later due slot. The old ordering would retry
        // the earlier missing receipt forever at a per-tick limit of one.
        await db.Users.Where(user => user.Id == _user).ExecuteUpdateAsync(setters => setters
            .SetProperty(user => user.LastModifiedAt, _due.AddDays(-1)));
        await db.Users.Where(user => user.Id == _other).ExecuteUpdateAsync(setters => setters
            .SetProperty(user => user.NextDigestAt, _due.AddDays(7)));
        _clock.Now = new(_due.AddDays(7).AddMinutes(30));
        async Task<ScheduleJobResult> Next()
        {
            await using var tickScope = _services.CreateAsyncScope();
            return Assert.Single((await tickScope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new RunScheduledTickCommand([_user, _other], 1))).Jobs);
        }
        var failed = await Next();
        Assert.Equal(_user, failed.UserId); Assert.Equal("email_receipt_missing", failed.Reason);
        var healthy = await Next();
        Assert.Equal(_other, healthy.UserId);
        Assert.Contains(healthy.Status, new[] { "advanced", "stopped" });
        // Advancing the clock a week intentionally makes the inventory stale;
        // fairness must still let this account reach its own generation checks.
        if (healthy.Status == "stopped") Assert.Equal("stale_or_unknown_storage_inventory", healthy.Reason);
    }

    [LocalPostgresMigrationFact]
    public async Task Process_FiniteTicksOnFutureAccount_EndsWithoutGenerating()
    {
        await using var scope = _services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        await db.Users.Where(user => user.Id == _user).ExecuteUpdateAsync(setters => setters.SetProperty(user => user.NextDigestAt, _due.AddDays(7)));
        using var worker = StartWorker(["--Schedule:MaxTicks", "2", "--Schedule:IntervalSeconds", "1"]);
        var output = worker.StandardOutput.ReadToEndAsync(); var error = worker.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await worker.WaitForExitAsync(timeout.Token); }
        finally { if (!worker.HasExited) { worker.Kill(entireProcessTree: true); await worker.WaitForExitAsync(); } }
        Assert.Equal(0, worker.ExitCode);
        var result = JsonSerializer.Deserialize<ScheduleRunResult>(await output)!;
        Assert.Equal("completed", result.Status); Assert.Equal(2, result.TicksCompleted);
        Assert.All(result.Ticks, tick => Assert.Empty(tick.Jobs)); Assert.Empty(await error);
        Assert.Empty(await db.WeeklyDigests.ToListAsync()); Assert.Empty(Directory.GetFiles(_email.SinkRoot!));
    }

    [LocalPostgresMigrationFact]
    public async Task MissingInventory_StopsWithoutAdvancing_AndExplicitReconcileAllowsRetry()
    {
        File.Delete(_storage.InventoryPath!);
        var stopped = await TickAsync(); Assert.Equal("stopped", stopped.Status);
        Assert.Equal("unknown_storage_inventory", Assert.Single(stopped.Jobs).Reason);
        await ReconcileAsync();
        var generated = await TickAsync();
        Assert.Equal("database_inventory_changed", Assert.Single(generated.Jobs).Reason);
        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            Assert.Equal(_due, (await db.Users.SingleAsync(user => user.Id == _user)).NextDigestAt);
            Assert.Equal(DigestStatus.Pending, (await db.WeeklyDigests.SingleAsync()).Status);
        }
        await CaptureAfterExplicitReconciliationAsync();
    }

    [LocalPostgresMigrationFact]
    public async Task Combined_ScheduledGenerationExportOptOutAndDeletion_PreservePrivacyBoundaries()
    {
        Assert.Equal("database_inventory_changed", Assert.Single((await TickAsync()).Jobs).Reason);
        await CaptureAfterExplicitReconciliationAsync();
        await using var scope = _services.CreateAsyncScope(); var sp = scope.ServiceProvider; var db = sp.GetRequiredService<DataContext>();
        var export = await sp.GetRequiredService<ISender>().Send(new GetUserDataExportQuery(_user));
        Assert.Equal("2.3", export.ExportVersion); Assert.Equal(_user, export.Account!.Id);
        Assert.Equal(_due.AddDays(7), export.Subscriber.NextDigestAt);
        var digest = Assert.Single(export.Digests); Assert.Equal(WeeklySchedulePolicy.Week(_due).Value, digest.Week);
        Assert.NotEmpty(digest.Recommendations); Assert.All(digest.Recommendations, pick => Assert.NotNull(pick.ScoreBreakdown.Snapshot));
        var copy = Assert.Single(export.LocalEmail!.Copies); Assert.Contains("capability-removed", copy.PlainText);
        Assert.DoesNotContain("fixture-only-not-a-real-credential", JsonSerializer.Serialize(export));
        var keyCount = await db.DataProtectionKeys.CountAsync();
        await sp.GetRequiredService<IUnsubscribeStore>().UnsubscribeAsync(_user, _clock.Now.UtcDateTime, default);
        _clock.Now = new(_due.AddDays(7).AddMinutes(30));
        Assert.Empty((await TickAsync()).Jobs); Assert.Single(await db.WeeklyDigests.ToListAsync());
        Assert.Equal(_due.AddDays(7), (await db.Users.SingleAsync(user => user.Id == _user)).NextDigestAt);
        await using (var lease = await sp.GetRequiredService<IAccountEmailLease>().AcquireAsync(_user, default))
        {
            await sp.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                await sp.GetRequiredService<ISender>().Send(new DeleteUserAccountCommand(_user), ct);
                await db.ApplicationUsers.Where(user => user.Id == _user).ExecuteDeleteAsync(ct);
                return true;
            });
            Assert.True((await sp.GetRequiredService<ILocalEmailArchive>().DeleteAsync(_user, default)).Complete);
        }
        Assert.Empty((await TickAsync()).Jobs); Assert.Empty(Directory.GetFiles(_email.SinkRoot!));
        Assert.False(await db.Users.IgnoreQueryFilters().AnyAsync(user => user.Id == _user));
        Assert.False(await db.WeeklyDigests.IgnoreQueryFilters().AnyAsync(d => d.UserId == _user));
        Assert.False(await db.WeeklyRecommendations.IgnoreQueryFilters().AnyAsync(r => r.UserId == _user));
        Assert.False(await db.TasteSignals.IgnoreQueryFilters().AnyAsync(s => s.UserId == _user));
        Assert.False(await db.UserTokens.AnyAsync(t => t.UserId == _user));
        Assert.True(await db.Users.AnyAsync(user => user.Id == _other)); Assert.Equal(keyCount, await db.DataProtectionKeys.CountAsync());
        Assert.NotEmpty(await db.Tracks.ToListAsync());
    }

    [ScheduledPostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessRestart_AfterPublicationOrCaptureBeforeAdvance_RecoversOriginalReceipt(bool captureCommit)
    {
        await PrepareCaptureAsync();
        await InstallBarrierAsync(captureCommit);
        _clock.Now = DateTimeOffset.UtcNow;
        await ReconcileAsync(); // Test-only trigger DDL changes the measured database inventory.
        await using var barrier = new NpgsqlConnection(_connection); await barrier.OpenAsync();
        await using (var hold = new NpgsqlCommand("SELECT pg_advisory_lock(1280200761)", barrier)) await hold.ExecuteNonQueryAsync();
        var started = await StartAtBarrierAsync();
        using var first = started.Process;
        var stdout = started.Output; var stderr = started.Error;
        string file;
        byte[] original;
        try
        {
            var blockedBackend = started.Backend;
            file = Assert.Single(Directory.GetFiles(_email.SinkRoot!, "*.eml.enc"));
            original = await File.ReadAllBytesAsync(file);
            using (var competing = StartWorker())
            {
                var competingOutput = competing.StandardOutput.ReadToEndAsync();
                var competingError = competing.StandardError.ReadToEndAsync();
                using var competingTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                try { await competing.WaitForExitAsync(competingTimeout.Token); }
                finally { if (!competing.HasExited) { competing.Kill(entireProcessTree: true); await competing.WaitForExitAsync(); } }
                Assert.Equal(2, competing.ExitCode);
                var busy = JsonSerializer.Deserialize<ScheduleRunResult>(await competingOutput)!;
                Assert.Equal("scheduler_busy", Assert.Single(busy.Ticks).Status);
                Assert.Empty(await competingError);
            }
            first.Kill(entireProcessTree: true); await first.WaitForExitAsync();
            // A backend blocked in a test trigger cannot observe client disconnect until
            // it resumes. Terminate this exact owned backend before releasing the barrier,
            // so an autocommit advance cannot finish after its client was killed.
            await using var terminate = new NpgsqlCommand("SELECT pg_terminate_backend(@pid, 5000)", barrier);
            terminate.Parameters.AddWithValue("pid", blockedBackend);
            await terminate.ExecuteScalarAsync();
        }
        finally
        {
            if (!first.HasExited) first.Kill(entireProcessTree: true);
            await first.WaitForExitAsync();
            Console.WriteLine(await stdout); Console.WriteLine(await stderr);
        }
        await using (var unlock = new NpgsqlCommand("SELECT pg_advisory_unlock(1280200761)", barrier)) await unlock.ExecuteNonQueryAsync();
        await DropBarrierAsync(captureCommit);
        await using (var scope = _services.CreateAsyncScope())
            Assert.Equal(_due, (await scope.ServiceProvider.GetRequiredService<DataContext>().Users.SingleAsync(user => user.Id == _user)).NextDigestAt);
        using var restarted = StartWorker();
        var output = restarted.StandardOutput.ReadToEndAsync(); var error = restarted.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try { await restarted.WaitForExitAsync(timeout.Token); }
        finally { if (!restarted.HasExited) restarted.Kill(entireProcessTree: true); }
        Assert.Equal(0, restarted.ExitCode); Assert.Contains("already_captured", await output); await error;
        Assert.Equal(original, await File.ReadAllBytesAsync(file)); Assert.Single(Directory.GetFiles(_email.SinkRoot!, "*.eml.enc"));
        await using var check = _services.CreateAsyncScope();
        Assert.Equal(_due.AddDays(7), (await check.ServiceProvider.GetRequiredService<DataContext>().Users.SingleAsync(user => user.Id == _user)).NextDigestAt);
    }

    private Process StartWorker(string[]? extraArguments = null)
    {
        var info = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        info.ArgumentList.Add(typeof(LocalDigestWorkerRunner).Assembly.Location);
        foreach (var argument in new[] { "--action", "schedule", "--origin", "Synthetic", "--Schedule:Users:0", _user.ToString() }) info.ArgumentList.Add(argument);
        foreach (var argument in extraArguments ?? []) info.ArgumentList.Add(argument);
        info.Environment["DOTNET_ENVIRONMENT"] = "Development";
        info.Environment["ConnectionStrings__DefaultConnection"] = _connection;
        info.Environment["Generation__Origin"] = "Synthetic";
        info.Environment["LocalEmail__Enabled"] = "true"; info.Environment["LocalEmail__ApplicationOrigin"] = _email.ApplicationOrigin;
        info.Environment["LocalEmail__SinkRoot"] = _email.SinkRoot;
        info.Environment["GenerationStorage__InventoryPath"] = _storage.InventoryPath;
        info.Environment["GenerationStorage__LeasePath"] = _storage.LeasePath;
        info.Environment["GenerationStorage__DatabaseOverheadBytesPerPick"] = "1000000";
        foreach (var pair in _storage.ArtifactRoots) info.Environment[$"GenerationStorage__ArtifactRoots__{pair.Key}__0"] = pair.Value[0];
        return Process.Start(info)!;
    }
    private async Task<(Process Process, Task<string> Output, Task<string> Error, int Backend)> StartAtBarrierAsync()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var process = StartWorker();
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            try { return (process, output, error, await WaitForBarrierAsync(process)); }
            catch
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                var result = JsonSerializer.Deserialize<ScheduleRunResult>(await output)!;
                Assert.Empty(await error); process.Dispose();
                Assert.Equal("database_inventory_changed", Assert.Single(Assert.Single(result.Ticks).Jobs).Reason);
                Assert.Empty(Directory.GetFiles(_email.SinkRoot!));
                await using var scope = _services.CreateAsyncScope();
                Assert.Equal(_due, (await scope.ServiceProvider.GetRequiredService<DataContext>().Users.SingleAsync(u => u.Id == _user)).NextDigestAt);
                _clock.Now = DateTimeOffset.UtcNow;
                await ReconcileAsync(); // Explicit operator retry after a guarded pre-publication stop.
            }
        }
        throw new InvalidOperationException("Worker did not reach the barrier after three explicit inventory reconciliations.");
    }

    private async Task InstallBarrierAsync(bool captureCommit)
    {
        await using var connection = new NpgsqlConnection(_connection); await connection.OpenAsync();
        var target = captureCommit ? "\"WeeklyDigests\"" : "\"Users\"";
        var column = captureCommit ? "\"Status\"" : "\"NextDigestAt\"";
        var condition = captureCommit ? "NEW.\"Status\" = 'LocalCaptured'" : "NEW.\"NextDigestAt\" IS DISTINCT FROM OLD.\"NextDigestAt\"";
        await using var command = new NpgsqlCommand($"""
            CREATE FUNCTION phase8_barrier() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN PERFORM pg_advisory_xact_lock(1280200761); RETURN NEW; END; $$;
            CREATE TRIGGER phase8_barrier BEFORE UPDATE OF {column} ON {target}
            FOR EACH ROW WHEN ({condition}) EXECUTE FUNCTION phase8_barrier();
            """, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task DropBarrierAsync(bool captureCommit)
    {
        await using var connection = new NpgsqlConnection(_connection); await connection.OpenAsync();
        var target = captureCommit ? "\"WeeklyDigests\"" : "\"Users\"";
        await using var command = new NpgsqlCommand($"DROP TRIGGER phase8_barrier ON {target}; DROP FUNCTION phase8_barrier()", connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<int> WaitForBarrierAsync(Process process)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline && !process.HasExited)
        {
            await using var connection = new NpgsqlConnection(_connection); await connection.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT pid FROM pg_locks WHERE locktype='advisory' AND objid=1280200761 AND database=(SELECT oid FROM pg_database WHERE datname=current_database()) AND NOT granted LIMIT 1", connection);
            if (await command.ExecuteScalarAsync() is int pid) return pid;
            await Task.Delay(30);
        }
        if (!process.HasExited) process.Kill(entireProcessTree: true);
        throw new InvalidOperationException("scheduled worker did not reach the controlled durable-state barrier");
    }

    public async Task DisposeAsync()
    {
        if (_services is not null) await _services.DisposeAsync();
        if (_admin.Length != 0)
        {
            await using var connection = new NpgsqlConnection(_admin); await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_name}\" WITH (FORCE)", connection);
            await command.ExecuteNonQueryAsync();
        }
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private static DateTime CurrentHour()
    {
        var now = DateTime.UtcNow;
        return now.Date.AddHours(now.Hour);
    }
    private sealed class Clock : TimeProvider { public DateTimeOffset Now; public override DateTimeOffset GetUtcNow() => Now; }
}

public sealed class ScheduledPostgresTheoryAttribute : TheoryAttribute
{
    public ScheduledPostgresTheoryAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LINER_PHASE1_POSTGRES")))
            Skip = "Set LINER_PHASE1_POSTGRES for disposable local process recovery checks.";
    }
}
