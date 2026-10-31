using LinerNotes.Application;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Services;
using LinerNotes.DataAccess;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.DataAccess.IdentityEntities;
using LinerNotes.DataAccess.Persistence;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using LinerNotes.Infrastructure;
using LinerNotes.Infrastructure.Storage;
using LinerNotes.Presentation.Controllers;
using LinerNotes.Presentation.Filters;
using LinerNotes.Presentation.Models;
using LinerNotes.Presentation.Options;
using LinerNotes.Presentation.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace LinerNotes.Presentation.Tests;

public sealed partial class CoordinatedAuthenticationPostgresTests : IAsyncLifetime
{
    private const string Password = "Coordinated-fixture-only-123!";
    private readonly string database = "liner_auth_coord_" + Guid.NewGuid().ToString("N");
    private readonly string root = Path.Combine(Path.GetTempPath(), "liner-auth-coord-" + Guid.NewGuid().ToString("N"));
    private string admin = "", connection = "";
    private ServiceProvider services = null!;
    private readonly HashObservation observation = new();
    private readonly MutableSpace space = new();

    public async Task InitializeAsync()
    {
        admin = Environment.GetEnvironmentVariable("LINER_PHASE1_POSTGRES") ?? "";
        if (admin.Length == 0) return;
        var settings = new NpgsqlConnectionStringBuilder(admin);
        Assert.Contains(settings.Host, new[] { "localhost", "127.0.0.1" });
        await using (var sql = new NpgsqlConnection(admin))
        {
            await sql.OpenAsync(); await new NpgsqlCommand($"CREATE DATABASE {database}", sql).ExecuteNonQueryAsync();
        }
        settings.Database = database; connection = settings.ConnectionString;
        Directory.CreateDirectory(root);
        var storage = new GenerationStorageOptions
        {
            InventoryPath = Path.Combine(root, "inventory.json"),
            LeasePath = Path.Combine(root, "lease"),
            DatabaseOverheadBytesPerPick = 1_000_000,
            ArtifactRoots = LocalGenerationStorage.Categories.ToDictionary(category => category, category => new[] { Path.Combine(root, category) })
        };
        foreach (var path in storage.ArtifactRoots.Values.SelectMany(paths => paths)) Directory.CreateDirectory(path);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connection,
            ["Jwt:SecretKey"] = "auth-coordination-fixture-only-signing-key-123456789",
            ["Jwt:Issuer"] = "fixture",
            ["Jwt:Audience"] = "fixture",
            ["LastFm:Mode"] = "FixtureOnly",
            ["Generation:Origin"] = "Synthetic"
            ,
            ["DiscoveryRuntime:EnforceStorageAdmission"] = "true"
        }).Build();
        var registrations = new ServiceCollection().AddLogging().AddApplication().AddDataAccess(config).AddInfrastructure(config).AddPresentation(config);
        // Reset-token tests must never write the operator's default filesystem key ring.
        registrations.AddDataProtection().UseEphemeralDataProtectionProvider();
        registrations.AddSingleton(storage);
        registrations.AddSingleton(new CoordinatedStorageOptions { FilesystemPaths = [root] });
        registrations.AddSingleton(observation);
        registrations.AddSingleton<IStorageFreeSpaceProbe>(space);
        registrations.AddScoped<PasswordHasher<ApplicationUser>, ObservedHasher>();
        services = registrations.BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        var state = await scope.ServiceProvider.GetRequiredService<IDigestGenerationStore>().ReadStorageAsync();
        await scope.ServiceProvider.GetRequiredService<LocalGenerationStorage>().ReconcileAsync(state);
    }

    public async Task DisposeAsync()
    {
        observation.Release.Set();
        if (services is not null) await services.DisposeAsync();
        if (admin.Length > 0)
        {
            await using var sql = new NpgsqlConnection(admin); await sql.OpenAsync();
            await new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", sql).ExecuteNonQueryAsync();
        }
        if (Directory.Exists(root)) Directory.Delete(root, true);
        observation.Release.Dispose();
    }

    private async Task<Guid> SeedAsync()
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IDiscoveryWriteCoordinator>().ExecuteAsync(1_000_000, async ct =>
        {
            var id = await scope.ServiceProvider.GetRequiredService<IAuthService>().RegisterAsync("fixture", "fixture@example.test", Password);
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            db.Users.Add(new User("fixture@example.test", "UTC", DigestDeliveryDay.Sunday, 8, id));
            await db.SaveChangesAsync(ct); return id;
        }, default);
    }

    private async Task<bool> LoginAsync(string password, bool failAfterIdentity = false)
    {
        await using var scope = services.CreateAsyncScope(); var provider = scope.ServiceProvider;
        var controller = new AuthController(provider.GetRequiredService<IAuthService>(), provider.GetRequiredService<IJwtService>(),
            Microsoft.Extensions.Options.Options.Create(new JwtOptions()), provider.GetRequiredService<IUnitOfWork>());
        var http = new DefaultHttpContext { RequestServices = provider }; http.Request.Method = "POST";
        var context = new ActionExecutingContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), [],
            new Dictionary<string, object?> { ["request"] = new LoginRequest("fixture@example.test", password) }, controller);
        bool accepted = false;
        await provider.GetRequiredService<DiscoveryWriteFilter>().OnActionExecutionAsync(context, async () =>
        {
            Assert.NotNull(provider.GetRequiredService<DataContext>().Database.CurrentTransaction);
            var identity = await provider.GetRequiredService<IAuthService>().CheckPasswordAsync("fixture@example.test", password);
            if (identity is not null)
            {
                await provider.GetRequiredService<IAuthService>().StoreRefreshTokenAsync(identity.Value.UserId, "fixture-refresh-" + Guid.NewGuid().ToString("N"));
                accepted = true;
            }
            if (failAfterIdentity) throw new InvalidOperationException("injected identity rollback");
            return new ActionExecutedContext(context, [], controller) { Result = new OkResult() };
        });
        return accepted;
    }

    [PostgresFact]
    public async Task PasswordCpuDoesNotHoldStorageAdmissionAndIdentityStillOwnsLockout()
    {
        var id = await SeedAsync(); observation.Enabled = true; observation.Pause = true;
        var login = LoginAsync(Password);
        try
        {
            await observation.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await using var other = services.CreateAsyncScope();
            await other.ServiceProvider.GetRequiredService<IDiscoveryWriteCoordinator>().ExecuteAsync(8192, _ => Task.FromResult(true), default)
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(login.IsCompleted);
        }
        finally { observation.Release.Set(); }
        Assert.True(await login); Assert.Equal(0, observation.HashesInsideTransaction);
        await using var check = services.CreateAsyncScope();
        Assert.Equal(0, (await check.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(id.ToString()))!.AccessFailedCount);
        await check.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
    }

    [PostgresFact]
    public async Task ParallelFailedAttemptsReachTheActualIdentityLockoutThreshold()
    {
        var id = await SeedAsync(); observation.Enabled = true;
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => LoginAsync("Wrong-fixture-only-123!")));
        Assert.All(results, Assert.False);
        await using var check = services.CreateAsyncScope();
        var manager = check.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await manager.FindByIdAsync(id.ToString());
        Assert.True(await manager.IsLockedOutAsync(user!));
        Assert.False(await LoginAsync(Password)); Assert.Equal(0, observation.HashesInsideTransaction);
        await check.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
    }

    [PostgresFact]
    public async Task IdentityWritesAndSessionCreationRollbackTogether()
    {
        var id = await SeedAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => LoginAsync(Password, failAfterIdentity: true));
        await using var check = services.CreateAsyncScope();
        Assert.Empty(await check.ServiceProvider.GetRequiredService<DataContext>().UserTokens.Where(token => token.UserId == id).ToArrayAsync());
        await check.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
    }

    [PostgresFact]
    public async Task StoragePressureRejectsAuthenticationWithoutCommittingFailureOrSessionWrites()
    {
        var id = await SeedAsync(); observation.Release.Set(); space.Bytes = 0;
        foreach (var password in new[] { Password, "Wrong-fixture-only-123!" })
            await Assert.ThrowsAsync<GenerationStoppedException>(() => LoginAsync(password));
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            Assert.Equal(0, (await db.Set<ApplicationUser>().AsNoTracking().SingleAsync(user => user.Id == id)).AccessFailedCount);
            Assert.Empty(await db.UserTokens.Where(token => token.UserId == id).ToArrayAsync());
            await scope.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
        }
        space.Bytes = long.MaxValue;
        for (int count = 0; count < 5; count++) Assert.False(await LoginAsync("Wrong-fixture-only-123!"));
        Assert.False(await LoginAsync(Password));
    }
    private sealed class MutableSpace : IStorageFreeSpaceProbe
    {
        public long Bytes = long.MaxValue;
        public long AvailableBytes(string path) => Bytes;
    }
    private sealed class HashObservation
    {
        public bool Enabled, Pause;
        public int HashesInsideTransaction;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release { get; } = new(false);
    }
    private sealed class ObservedHasher(DataContext db, HashObservation observation) : PasswordHasher<ApplicationUser>
    {
        public override PasswordVerificationResult VerifyHashedPassword(ApplicationUser user, string hash, string password)
        {
            if (observation.Enabled)
            {
                if (db.Database.CurrentTransaction is not null) Interlocked.Increment(ref observation.HashesInsideTransaction);
                observation.Started.TrySetResult();
                if (observation.Pause && !observation.Release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("test hash gate");
            }
            return base.VerifyHashedPassword(user, hash, password);
        }
    }
}
