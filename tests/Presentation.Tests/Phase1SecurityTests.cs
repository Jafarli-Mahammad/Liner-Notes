using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LinerNotes.Application;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Services;
using LinerNotes.DataAccess;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.DataAccess.IdentityEntities;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Taste;
using LinerNotes.Presentation;
using LinerNotes.Presentation.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace LinerNotes.Presentation.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LINER_PHASE1_POSTGRES")))
            Skip = "Explicit local PostgreSQL run: set LINER_PHASE1_POSTGRES.";
    }
}

// Uses a uniquely named disposable database, never an application database.
public sealed class Phase1SecurityTests : IAsyncLifetime
{
    private string _admin = "";
    private string _connection = "";
    private readonly string _database = "liner_phase1_" + Guid.NewGuid().ToString("N");
    private ServiceProvider _services = null!;
    private readonly TestClock _clock = new();
    private const string Password = "LocalTestPassword123!";
    private const string SigningKey = "phase1-test-key-only-not-a-deployment-secret-123456";

    public async Task InitializeAsync()
    {
        _admin = Environment.GetEnvironmentVariable("LINER_PHASE1_POSTGRES")!;
        var source = new NpgsqlConnectionStringBuilder(_admin);
        if (source.Host is not ("localhost" or "127.0.0.1"))
            throw new InvalidOperationException("Phase 1 tests require an explicitly local database.");
        await using var connection = new NpgsqlConnection(_admin);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE {_database}", connection);
        await command.ExecuteNonQueryAsync();
        source.Database = _database;
        _connection = source.ConnectionString;
        var services = new ServiceCollection().AddLogging();
        var config = Configuration(_connection);
        services.AddApplication().AddDataAccess(config).AddPresentation(config);
        services.AddSingleton<TimeProvider>(_clock);
        _services = services.BuildServiceProvider();
        await using var scope = _services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DataContext>().Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_services is not null) await _services.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        if (string.IsNullOrEmpty(_admin)) return;
        await using var connection = new NpgsqlConnection(_admin);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS {_database} WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static IConfiguration Configuration(string connection) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connection,
            ["Jwt:SecretKey"] = SigningKey,
            ["Jwt:ExpiryMinutes"] = "15",
            ["LastFm:Mode"] = "FixtureOnly"
        }).Build();

    private TestApp App(string environment = "Development") => new(_connection, environment, _clock);

    private async Task<Guid> SeedUser(string name)
    {
        await using var scope = _services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        return await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var id = await service.RegisterAsync(name, name + "@example.test", Password);
            db.Users.Add(new User(name + "@example.test", "UTC", id: id));
            await db.SaveChangesAsync(ct);
            await service.StoreRefreshTokenAsync(id, "original-refresh-token");
            return id;
        });
    }

    [PostgresFact]
    public async Task Registration_RollsBackCredentialsProfileAndSeeds_WhenValidationFails()
    {
        await using var app = App();
        using var client = app.CreateClient();
        var result = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("badregistration", "bad@example.test", Password, DeliveryHourUtc: 99));
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        var seedFailure = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("badseeds", "badseeds@example.test", Password,
            SeedTags: Enumerable.Range(0, 6).Select(i => new LinerNotes.Application.DTOs.Taste.SeedTagDto("tag" + i)).ToArray()));
        Assert.Equal(HttpStatusCode.BadRequest, seedFailure.StatusCode);
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        Assert.Empty(await db.ApplicationUsers.ToListAsync());
        Assert.Empty(await db.Users.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await db.TasteSignals.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await db.UserTokens.ToListAsync());
    }

    [PostgresFact]
    public async Task Refresh_HashesExpiresBindsToAccount_AndAllowsOnlyOneConcurrentRedemption()
    {
        var id = await SeedUser("rotation");
        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            Assert.DoesNotContain("original-refresh-token", (await db.UserTokens.SingleAsync()).Value!);
            var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
            Assert.False(await auth.ValidateRefreshTokenAsync(Guid.NewGuid(), "original-refresh-token"));
            Assert.False(await auth.ValidateRefreshTokenAsync(id, "wrong"));
        }
        async Task<bool> Rotate(string replacement)
        {
            await using var scope = _services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IAuthService>()
                .RotateRefreshTokenAsync(id, "original-refresh-token", replacement);
        }
        var results = await Task.WhenAll(Rotate("replacement-one"), Rotate("replacement-two"));
        Assert.Single(results, x => x);
        await using var check = _services.CreateAsyncScope();
        var service = check.ServiceProvider.GetRequiredService<IAuthService>();
        Assert.False(await service.ValidateRefreshTokenAsync(id, "original-refresh-token"));
        var winner = results[0] ? "replacement-one" : "replacement-two";
        Assert.True(await service.ValidateRefreshTokenAsync(id, winner));
        _clock.Now = _clock.Now.AddDays(8);
        Assert.False(await service.ValidateRefreshTokenAsync(id, winner));
    }

    [PostgresFact]
    public async Task Refresh_RejectsLegacyPlaintextAndSecurityStampChanges()
    {
        var id = await SeedUser("stamp");
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var service = scope.ServiceProvider.GetRequiredService<IAuthService>();
        await db.ApplicationUsers.Where(u => u.Id == id).ExecuteUpdateAsync(s => s.SetProperty(u => u.SecurityStamp, "changed"));
        Assert.False(await service.ValidateRefreshTokenAsync(id, "original-refresh-token"));
        await db.UserTokens.Where(t => t.UserId == id).ExecuteUpdateAsync(s => s.SetProperty(t => t.Value, "old-plaintext-token"));
        Assert.False(await service.ValidateRefreshTokenAsync(id, "old-plaintext-token"));
    }

    [PostgresFact]
    public async Task Login_LocksOutAfterFiveFailures_AndRateLimitsUnknownAccounts()
    {
        await SeedUser("lockout");
        for (var i = 0; i < 5; i++)
        {
            await using var scope = _services.CreateAsyncScope();
            Assert.Null(await scope.ServiceProvider.GetRequiredService<IAuthService>().CheckPasswordAsync("lockout@example.test", "wrong"));
        }
        await using (var scope = _services.CreateAsyncScope())
        {
            Assert.Null(await scope.ServiceProvider.GetRequiredService<IAuthService>().CheckPasswordAsync("lockout@example.test", Password));
            var user = await scope.ServiceProvider.GetRequiredService<DataContext>().ApplicationUsers.SingleAsync();
            Assert.True(user.LockoutEnd > DateTimeOffset.UtcNow);
        }
        await using var app = App();
        using var client = app.CreateClient();
        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("missing@example.test", "wrong"))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("missing@example.test", "wrong"))).StatusCode);
    }

    [PostgresFact]
    public async Task Deletion_RemovesHiddenOwnedRows_PreservesCatalogAndOtherAccounts_RevokesAccess()
    {
        var id = await SeedUser("delete");
        var other = await SeedUser("other");
        string access;
        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var digest = new WeeklyDigest(id, new IsoWeek(2026, 40), DateTime.UtcNow, DateTime.UtcNow.AddDays(7)) { IsDeleted = true };
            var track = Track.Create("Keep shared catalog", "Shared Artist");
            var rec = digest.AddRecommendation(track, new ScoreBreakdown(), 1);
            rec.IsDeleted = true;
            db.WeeklyDigests.Add(digest);
            db.TasteSignals.Add(new TasteSignal(id, LinerNotes.Domain.Enums.TasteTargetType.Tag, "test", 1,
                LinerNotes.Domain.Enums.TasteSignalSource.InitialSeedManual, "test") { IsDeleted = true });
            await db.SaveChangesAsync();
            access = scope.ServiceProvider.GetRequiredService<IJwtService>().GenerateAccessToken(id, "delete", "delete@example.test");
        }
        await using var app = App();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/subscribers/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        await using var verify = _services.CreateAsyncScope();
        var context = verify.ServiceProvider.GetRequiredService<DataContext>();
        Assert.False(await context.Users.IgnoreQueryFilters().AnyAsync(u => u.Id == id));
        Assert.False(await context.ApplicationUsers.AnyAsync(u => u.Id == id));
        Assert.False(await context.WeeklyDigests.IgnoreQueryFilters().AnyAsync(d => d.UserId == id));
        Assert.False(await context.WeeklyRecommendations.IgnoreQueryFilters().AnyAsync(r => r.UserId == id));
        Assert.False(await context.TasteSignals.IgnoreQueryFilters().AnyAsync(s => s.UserId == id));
        Assert.False(await context.UserTokens.AnyAsync(t => t.UserId == id));
        Assert.True(await context.Users.AnyAsync(u => u.Id == other));
        Assert.Single(await context.Tracks.ToListAsync());
        Assert.False(await verify.ServiceProvider.GetRequiredService<IAuthService>().ValidateRefreshTokenAsync(id, "original-refresh-token"));
    }

    [PostgresFact]
    public async Task Transaction_RollsBackDeletionAndRegistration_WhenLaterWorkFailsOrIsCancelled()
    {
        var id = await SeedUser("rollback");
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var repository = scope.ServiceProvider.GetRequiredService<LinerNotes.Application.Common.Interfaces.Repositories.IUserRepository>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.ExecuteInTransactionAsync<bool>(async ct =>
        {
            await repository.DeleteOwnedDataAsync(id, ct);
            throw new InvalidOperationException("Injected credential deletion failure");
        }));
        Assert.True(await db.Users.AnyAsync(u => u.Id == id));
        Assert.True(await db.UserTokens.AnyAsync(t => t.UserId == id));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => uow.ExecuteInTransactionAsync<bool>(async ct =>
        {
            await scope.ServiceProvider.GetRequiredService<IAuthService>().RegisterAsync("cancelled", "cancelled@example.test", Password);
            throw new OperationCanceledException();
        }));
        Assert.False(await db.ApplicationUsers.AnyAsync(u => u.UserName == "cancelled"));
    }

    [PostgresFact]
    public async Task Feedback_SerializesConcurrentReplacement_AndRollsBackOnFailure()
    {
        var id = await SeedUser("feedback");
        Guid recId;
        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var digest = new WeeklyDigest(id, new IsoWeek(2026, 40), DateTime.UtcNow, DateTime.UtcNow.AddDays(7));
            var rec = digest.AddRecommendation(Track.Create("Feedback track", "Feedback artist"),
                new ScoreBreakdown { MatchedTags = new[] { new MatchedTagContribution("rock", 1, 1, 1) } }, 1);
            recId = rec.Id;
            db.WeeklyDigests.Add(digest);
            await db.SaveChangesAsync();
        }
        async Task Replace(LinerNotes.Domain.Enums.UserFeedback feedback)
        {
            await using var scope = _services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<MediatR.ISender>().Send(
                new LinerNotes.Application.Features.Digests.Commands.RecordFeedback.RecordRecommendationFeedbackCommand(recId, id, feedback));
        }
        await Task.WhenAll(Replace(LinerNotes.Domain.Enums.UserFeedback.Liked), Replace(LinerNotes.Domain.Enums.UserFeedback.Liked));
        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            Assert.Equal(2, await db.TasteSignals.IgnoreQueryFilters().CountAsync(s => s.UserId == id));
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => uow.ExecuteInTransactionAsync<bool>(async ct =>
            {
                await scope.ServiceProvider.GetRequiredService<MediatR.ISender>().Send(
                    new LinerNotes.Application.Features.Digests.Commands.RecordFeedback.RecordRecommendationFeedbackCommand(recId, id, LinerNotes.Domain.Enums.UserFeedback.Disliked), ct);
                throw new InvalidOperationException("Injected failure after feedback save");
            }));
            Assert.Equal(LinerNotes.Domain.Enums.UserFeedback.Liked, (await db.WeeklyRecommendations.SingleAsync()).Feedback);
            Assert.Equal(2, await db.TasteSignals.IgnoreQueryFilters().CountAsync(s => s.UserId == id));
        }
        await Replace(LinerNotes.Domain.Enums.UserFeedback.Disliked);
        await using var verify = _services.CreateAsyncScope();
        var signals = await verify.ServiceProvider.GetRequiredService<DataContext>().TasteSignals.IgnoreQueryFilters().ToListAsync();
        var signal = Assert.Single(signals);
        Assert.Equal(LinerNotes.Domain.Enums.TasteTargetType.Track, signal.TargetType);
        Assert.True(signal.Weight < 0);
        await using (var scope = _services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<MediatR.ISender>().Send(
                new LinerNotes.Application.Features.Digests.Commands.RecordFeedback.RecordRecommendationFeedbackCommand(
                    recId, id, LinerNotes.Domain.Enums.UserFeedback.AlreadyKnown, Rating: 9));
        }
        await using var familiar = _services.CreateAsyncScope();
        var knownSignals = await familiar.ServiceProvider.GetRequiredService<DataContext>().TasteSignals
            .IgnoreQueryFilters().Where(s => s.UserId == id).ToListAsync();
        Assert.Contains(knownSignals, s => s.Source == LinerNotes.Domain.Enums.TasteSignalSource.RecommendationAlreadyKnown
            && s.TargetType == LinerNotes.Domain.Enums.TasteTargetType.Track);
        Assert.Contains(knownSignals, s => s.Weight > 0 && s.TargetType == LinerNotes.Domain.Enums.TasteTargetType.Artist);
        Assert.DoesNotContain(knownSignals, s => s.Weight < 0);
    }

    [PostgresFact]
    public async Task Transport_DeniesForeignOriginsAndInlineScripts_AndRequiresProductionHttps()
    {
        await using var app = App();
        using var client = app.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        request.Headers.Add("Origin", "https://untrusted.example");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        var cors = await client.SendAsync(request);
        Assert.False(cors.Headers.Contains("Access-Control-Allow-Origin"));
        var page = await client.GetAsync("/");
        Assert.Contains("script-src 'self';", page.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", page.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", page.Headers.GetValues("X-Frame-Options").Single());
        Assert.DoesNotContain("onclick=", await page.Content.ReadAsStringAsync());
        var api = await client.GetAsync("/api/auth/me");
        Assert.True(api.Headers.CacheControl?.NoStore);
        await using var production = App("Production");
        using var prod = production.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var redirect = await prod.GetAsync("/");
        Assert.Equal(HttpStatusCode.TemporaryRedirect, redirect.StatusCode);
        Assert.Equal("https", redirect.Headers.Location?.Scheme);
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class TestApp(string connection, string environment, TimeProvider clock) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            foreach (var setting in Configuration(connection).AsEnumerable())
                builder.UseSetting(setting.Key, setting.Value);
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, config) => config.AddConfiguration(Configuration(connection)));
            builder.ConfigureServices(services => services.AddSingleton(clock));
        }
    }
}
