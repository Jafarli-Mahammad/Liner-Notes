using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using LinerNotes.Application;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.Application.Features.Digests.Commands.CaptureLocalDigest;
using LinerNotes.Application.Features.Export.Queries.GetUserDataExport;
using LinerNotes.Application.Services;
using LinerNotes.DataAccess;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.DataAccess.IdentityEntities;
using LinerNotes.DataAccess.Persistence;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Common;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Taste;
using LinerNotes.Infrastructure;
using LinerNotes.Infrastructure.Email;
using LinerNotes.Infrastructure.Storage;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSubstitute;
using Xunit;

namespace LinerNotes.Presentation.Tests;

public sealed class Phase8PrivacyApiTests : IAsyncLifetime
{
    private readonly string _database = "liner_phase8_api_" + Guid.NewGuid().ToString("N");
    private readonly string _root = Path.Combine(Path.GetTempPath(), "liner-phase8-api-" + Guid.NewGuid().ToString("N"));
    private readonly Guid _owner = Guid.NewGuid();
    private readonly Guid _other = Guid.NewGuid();
    private readonly Guid _role = Guid.NewGuid();
    private readonly IsoWeek _week = new(2026, 42);
    private string _admin = "";
    private IConfiguration _configuration = null!;
    private ServiceProvider _services = null!;
    private LocalEmailOptions _email = null!;
    private GenerationStorageOptions _storage = null!;

    public async Task InitializeAsync()
    {
        _admin = Environment.GetEnvironmentVariable("LINER_PHASE1_POSTGRES") ?? "";
        if (_admin.Length == 0) return;
        var settings = new NpgsqlConnectionStringBuilder(_admin);
        Assert.Contains(settings.Host, new[] { "localhost", "127.0.0.1", "::1" });
        await using var connection = new NpgsqlConnection(_admin);
        await connection.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{_database}\"", connection))
            await create.ExecuteNonQueryAsync();
        settings.Database = _database;
        settings.Pooling = false;
        Directory.CreateDirectory(_root);
        _storage = new()
        {
            InventoryPath = Path.Combine(_root, "inventory.json"), LeasePath = Path.Combine(_root, "lease"),
            DatabaseOverheadBytesPerPick = 1_000_000,
            ArtifactRoots = LocalGenerationStorage.Categories.ToDictionary(c => c, c => new[] { Path.Combine(_root, c) })
        };
        foreach (var path in _storage.ArtifactRoots.Values.SelectMany(paths => paths)) Directory.CreateDirectory(path);
        _email = new() { Enabled = true, ApplicationOrigin = "http://127.0.0.1:5000", SinkRoot = Path.Combine(_root, "copies") };
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(_email.SinkRoot, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        _configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = settings.ConnectionString,
            ["Jwt:SecretKey"] = "phase8-fixture-signing-key-only-not-a-deployment-secret",
            ["Environment"] = "Development", ["LastFm:Mode"] = "FixtureOnly", ["Generation:Origin"] = "Synthetic",
            ["LocalEmail:Enabled"] = "true", ["LocalEmail:ApplicationOrigin"] = _email.ApplicationOrigin
        }).Build();
        var registrations = new ServiceCollection().AddLogging().AddApplication().AddDataAccess(_configuration)
            .AddInfrastructure(_configuration).AddPresentation(_configuration);
        registrations.AddSingleton(_email).AddSingleton(_storage);
        _services = registrations.BuildServiceProvider();
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        db.Roles.Add(new IdentityRole<Guid>("fixture-role") { Id = _role, NormalizedName = "FIXTURE-ROLE" });
        db.RoleClaims.Add(new() { RoleId = _role, ClaimType = "permission", ClaimValue = "fixture" });
        await db.SaveChangesAsync();
        await SeedAccountAsync(_owner, "owner");
        await SeedAccountAsync(_other, "other");
    }

    private async Task SeedAccountAsync(Guid id, string name)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        db.ApplicationUsers.Add(new ApplicationUser
        {
            Id = id, UserName = "fixture-" + name, NormalizedUserName = "PROFILE_" + name,
            Email = $"identity-{name}@example.test", NormalizedEmail = $"IDENTITY_{name}", EmailConfirmed = true,
            PhoneNumber = name == "owner" ? "+0005550108" : null, PhoneNumberConfirmed = true,
            PasswordHash = "credential-hash-sentinel-" + name, SecurityStamp = "credential-stamp-" + name,
            ConcurrencyStamp = "concurrency-sentinel-" + name, AccessFailedCount = 2, LockoutEnabled = true,
            LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(2), TwoFactorEnabled = true
        });
        var user = new User($"domain-{name}@example.test", "Asia/Baku", DigestDeliveryDay.Friday, 17, id);
        user.SetNextDigestAt(new(2026, 10, 16, 17, 0, 0, DateTimeKind.Utc));
        db.Users.Add(user);
        db.UserClaims.Add(new() { UserId = id, ClaimType = "authentication-fixture", ClaimValue = "claim-sentinel-" + name });
        db.UserLogins.Add(new() { UserId = id, LoginProvider = "fixture", ProviderKey = "login-sentinel-" + name, ProviderDisplayName = "fixture-auth" });
        db.UserRoles.Add(new() { UserId = id, RoleId = _role });
        db.TasteSignals.Add(TasteSignal.CreateSeedTag(id, "ROCK", 0.73, "privacy fixture " + name));
        var hidden = new UserMusicConnection(id, MusicServiceType.LastFm, "stored-username-" + name, "provider-token-sentinel-" + name)
        { IsDeleted = true, DeletedBy = id, DeletedAt = new(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc) };
        hidden.MarkSynced(new(2026, 10, 3, 1, 2, 3, DateTimeKind.Utc));
        hidden.Deactivate();
        db.UserMusicConnections.Add(hidden);
        var artist = new Artist("Shared " + name, "artist-mbid-" + name);
        db.Artists.Add(artist);
        db.Albums.Add(new Album("Shared album " + name, artist.Name, "album-mbid-" + name, artist.Id));
        var track = new Track("Sentinel title " + name, "Sentinel artist " + name, "Stored album", "track-mbid-" + name,
            281, "https://example.test/spotify", "https://example.test/youtube");
        var digest = new WeeklyDigest(id, _week, new(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc), new(2026, 10, 18, 23, 59, 59, DateTimeKind.Utc));
        var score = JsonSerializer.Deserialize<ScoreBreakdown>("""
            {"FinalScore":0.81,"TagSimilarityRaw":0.61,"TagSimilarityScore":0.51,"PopularityRaw":0.33,
            "PopularityPenalty":0,"NoveltyRaw":1,"NoveltyBoost":0.3,"FeedbackPenaltyRaw":0.1,
            "FeedbackPenalty":0,"MatchedTags":[{"TagName":"rock","CandidateTagWeight":0.9,
            "UserTasteWeight":0.73,"ContributionProduct":0.657,"FutureTag":{"keep":true}}],
            "FutureComponent":{"unchanged":[1,2,3]}}
            """)!;
        var response = new StoredResponse("Fixture", "fixture.tags", "Fixture seed", "Synthetic",
            new string('a', 64), new DateTimeOffset(2026, 10, 2, 3, 4, 5, TimeSpan.Zero))
        { UnknownFields = Future("futureResponse") };
        var observation = new StoredObservation("12", "12.0", null) { UnknownFields = Future("futureObservation") };
        var tag = new StoredTag("rock", observation, "https://example.test/tag", response, null, 2)
        { UnknownFields = Future("futureTag") };
        var evidence = new ScoreEvidence(
            [new StoredTasteSignal(Guid.NewGuid(), "Tag", "ROCK", 0.73, "InitialSeedManual", "snapshot fixture") { UnknownFields = Future("futureSignal") }],
            [new StoredSeed("Fixture seed", new Dictionary<string, double> { ["rock"] = 0.87 }, [tag], response, null,
                [new StoredGap("Fixture seed", "fixture.missing", "not_supplied", response) { UnknownFields = Future("futureSeedGap") }])
                { UnknownFields = Future("futureSeed") }],
            [tag], response,
            [new StoredDiscoveryPath("Fixture seed", observation, response, response, 3, "https://example.test/track",
                "https://example.test/artist", observation, "3", "Fixture discovery", 4) { UnknownFields = Future("futurePath") }],
            [new StoredGap("Fixture seed", "fixture.missing", "not_supplied", null) { UnknownFields = Future("futureGap") }],
            "artist", "not_supplied") { UnknownFields = Future("futureEvidence") };
        score = score with { Snapshot = new ScoreSnapshot
        {
            ConfigurationVersion = "phase8-fixture-v1", ConfigurationSha256 = new string('b', 64),
            ConfigurationPolicy = new Dictionary<string, string> { ["scope"] = "synthetic" },
            Weights = new(1, 0.2) { UnknownFields = Future("futureWeights") },
            TrackKey = track.TrackKey, ArtistName = track.ArtistName, Title = track.Title, Week = _week.Value,
            TasteWeights = new Dictionary<string, double> { ["rock"] = 0.73 }, CandidateWeights = new Dictionary<string, double> { ["rock"] = 0.9 },
            TasteMagnitude = 0.73, CandidateMagnitude = 0.9, Familiarity = false,
            Contributions = [new("rock", 0.9, 0.73, 1) { UnknownFields = Future("futureContribution") }],
            Components = [new("tag", 0.61) { UnknownFields = Future("futureComponent") }],
            MissingReasons = ["popularity_not_used"], Evidence = evidence, UnknownFields = Future("futureSnapshot")
        } };
        var recommendation = digest.AddRecommendation(track, score, 1);
        recommendation.RecordFeedback(UserFeedback.AlreadyKnown, "Stored feedback " + name, 8);
        db.WeeklyDigests.Add(digest);
        foreach (var entry in db.ChangeTracker.Entries<IAuditableEntity>())
        {
            entry.Entity.CreatedBy = Guid.NewGuid();
            entry.Entity.LastModifiedBy = Guid.NewGuid();
            entry.Entity.LastModifiedAt = new(2026, 10, 6, 1, 2, 3, DateTimeKind.Utc);
            entry.Entity.DeletedBy = Guid.NewGuid();
            entry.Entity.DeletedAt = new(2026, 10, 4, 4, 5, 6, DateTimeKind.Utc);
        }
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<IAuthService>().StoreRefreshTokenAsync(id, "refresh-fixture-" + name);
        // Seed imported/legacy text after Identity validation; export must still display it safely.
        await db.ApplicationUsers.Where(account => account.Id == id).ExecuteUpdateAsync(setters => setters
            .SetProperty(account => account.UserName, $"<img src=x onerror=alert(1)>-{name}"));
    }

    private static Dictionary<string, JsonElement> Future(string name) => new()
    {
        [name] = JsonSerializer.SerializeToElement(new { retained = new[] { 11, 22, 33 } })
    };

    private TestApp App(bool failCredentials = false, bool failExport = false) => new(_configuration, _email, _storage, failCredentials, failExport);

    private HttpClient AuthenticatedClient(TestApp app, Guid id)
    {
        var client = app.CreateClient();
        using var scope = _services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<IJwtService>().GenerateAccessToken(id, "fixture", "fixture@example.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [PostgresFact]
    public async Task Export_AllReviewedScalarsMatchPersistedValues_ExcludesSecretsAndOtherAccounts()
    {
        await using (var values = _services.CreateAsyncScope())
        {
            var context = values.ServiceProvider.GetRequiredService<DataContext>();
            await context.Users.Where(user => user.Id == _owner).ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.EmailUnsubscribedAtUtc, new DateTime(2026, 10, 7, 8, 9, 10, DateTimeKind.Utc)));
            await context.ApplicationUsers.Where(user => user.Id == _owner).ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.LastModifiedBy, Guid.NewGuid())
                .SetProperty(user => user.LastModifiedAt, new DateTime(2026, 10, 8, 9, 10, 11, DateTimeKind.Utc)));
            await context.WeeklyDigests.Where(digest => digest.UserId == _owner).ExecuteUpdateAsync(setters => setters
                .SetProperty(digest => digest.Status, DigestStatus.Sent)
                .SetProperty(digest => digest.SentAt, new DateTime(2026, 10, 9, 10, 11, 12, DateTimeKind.Utc))
                .SetProperty(digest => digest.ErrorMessage, "historical fixture error"));
        }
        await using var app = App();
        using var client = AuthenticatedClient(app, _owner);
        using var response = await client.GetAsync($"/api/export/my-data?userId={_other}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(text);
        var root = json.RootElement;
        Assert.Equal("2.3", root.GetProperty("exportVersion").GetString());
        Assert.Equal(_owner, root.GetProperty("account").GetProperty("id").GetGuid());
        foreach (var secret in new[] { "credential-hash-sentinel", "credential-stamp-", "concurrency-sentinel-", "provider-token-sentinel-", "claim-sentinel-", "login-sentinel-", "refresh-fixture-", "identity-other@", "Stored feedback other" })
            Assert.DoesNotContain(secret, text);
        foreach (var property in new[] { "passwordHash", "securityStamp", "concurrencyStamp", "encryptedToken", "accessFailedCount", "lockoutEnabled", "lockoutEnd", "twoFactorEnabled" })
            Assert.DoesNotContain('"' + property + '"', text);

        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var digest = await db.WeeklyDigests.SingleAsync(d => d.UserId == _owner);
        var rec = root.GetProperty("digests")[0].GetProperty("recommendations")[0];
        await AssertRowAsync(db, "public", "Users", _owner, root.GetProperty("subscriber"));
        await AssertRowAsync(db, "identity", "Users", _owner, root.GetProperty("account"),
            ["PasswordHash", "SecurityStamp", "ConcurrencyStamp", "AccessFailedCount", "LockoutEnabled", "LockoutEnd", "TwoFactorEnabled"]);
        await AssertRowAsync(db, "public", "UserMusicConnections", root.GetProperty("connections")[0].GetProperty("id").GetGuid(), root.GetProperty("connections")[0], ["EncryptedToken"]);
        await AssertRowAsync(db, "public", "TasteSignals", root.GetProperty("tasteSignals")[0].GetProperty("id").GetGuid(), root.GetProperty("tasteSignals")[0]);
        await AssertRowAsync(db, "public", "WeeklyDigests", digest.Id, root.GetProperty("digests")[0]);
        await AssertRowAsync(db, "public", "WeeklyRecommendations", rec.GetProperty("id").GetGuid(), rec);
        await AssertRowAsync(db, "public", "Tracks", rec.GetProperty("track").GetProperty("id").GetGuid(), rec.GetProperty("track"));
        Assert.True(root.GetProperty("connections")[0].GetProperty("isDeleted").GetBoolean());
        Assert.Equal("<img src=x onerror=alert(1)>-owner", root.GetProperty("account").GetProperty("userName").GetString());
        using var unauthenticated = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await unauthenticated.GetAsync("/api/export/my-data")).StatusCode);
        using var otherClient = AuthenticatedClient(app, _other);
        using var otherJson = JsonDocument.Parse(await otherClient.GetStringAsync("/api/export/my-data"));
        Assert.Equal(JsonValueKind.Null, otherJson.RootElement.GetProperty("account").GetProperty("phoneNumber").ValueKind);
    }

    [PostgresFact]
    public async Task AccountProfileReader_RejectsMissingAndHiddenIdentity()
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var reader = scope.ServiceProvider.GetRequiredService<IAccountProfileReader>();
        Assert.Null(await reader.GetAsync(Guid.NewGuid()));
        await db.ApplicationUsers.Where(account => account.Id == _other)
            .ExecuteUpdateAsync(setters => setters.SetProperty(account => account.IsDeleted, true));
        Assert.Null(await reader.GetAsync(_other));
        Assert.Equal(_owner, (await reader.GetAsync(_owner))!.Id);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    // Expected values come from persisted SQL columns, not from the mapper under test.
    private static async Task AssertRowAsync(DataContext db, string schema, string table, Guid id,
        JsonElement exported, string[]? excluded = null)
    {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"SELECT to_jsonb(row)::text FROM \"{schema}\".\"{table}\" row WHERE row.\"Id\" = @id";
        var parameter = command.CreateParameter(); parameter.ParameterName = "id"; parameter.Value = id; command.Parameters.Add(parameter);
        using var persisted = JsonDocument.Parse((string)(await command.ExecuteScalarAsync())!);
        foreach (var property in persisted.RootElement.EnumerateObject())
        {
            if (excluded?.Contains(property.Name) == true) continue;
            var name = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
            JsonElement actual = property.Name == "TrackId" ? exported.GetProperty("track").GetProperty("id") : exported.GetProperty(name);
            AssertJsonValue(property.Value, actual);
        }
    }

    private static void AssertJsonValue(JsonElement expected, JsonElement actual)
    {
        if (expected.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in expected.EnumerateObject())
            {
                var name = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
                if (!actual.TryGetProperty(name, out var value)) value = actual.GetProperty(property.Name); // Unknown JSON fields preserve their names.
                AssertJsonValue(property.Value, value);
            }
        }
        else if (expected.ValueKind == JsonValueKind.Array)
        {
            Assert.Equal(expected.GetArrayLength(), actual.GetArrayLength());
            for (var index = 0; index < expected.GetArrayLength(); index++) AssertJsonValue(expected[index], actual[index]);
        }
        else if (expected.ValueKind == JsonValueKind.String && expected.TryGetDateTimeOffset(out var date))
            Assert.Equal(date, actual.GetDateTimeOffset());
        else if (expected.ValueKind == JsonValueKind.Number)
            Assert.Equal(expected.GetDecimal(), actual.GetDecimal());
        else Assert.Equal(expected.ToString(), actual.ToString());
    }

    [PostgresFact]
    public async Task Export_AllHistoryPagesIncludeHiddenRowsAndTimestampTies()
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        await db.WeeklyDigests.Where(d => d.UserId == _owner).ExecuteDeleteAsync();
        var start = new DateTime(2010, 1, 4, 0, 0, 0, DateTimeKind.Utc);
        var ids = new HashSet<Guid>();
        for (var index = 0; index < 205; index++)
        {
            var date = start.AddDays(7 * index);
            var digest = new WeeklyDigest(_owner, new(System.Globalization.ISOWeek.GetYear(date), System.Globalization.ISOWeek.GetWeekOfYear(date)), date, date.AddDays(7))
            { IsDeleted = index % 2 == 0, DeletedBy = _owner, DeletedAt = date };
            digest.AddRecommendation(Track.Create("History " + index, "History artist"), new ScoreBreakdown(), 1).IsDeleted = true;
            ids.Add(digest.Id); db.WeeklyDigests.Add(digest);
        }
        await db.SaveChangesAsync();
        await using var app = App(); using var client = AuthenticatedClient(app, _owner);
        using var json = JsonDocument.Parse(await client.GetStringAsync("/api/export/my-data"));
        var digests = json.RootElement.GetProperty("digests").EnumerateArray().ToArray();
        Assert.Equal(205, digests.Length);
        Assert.True(ids.SetEquals(digests.Select(d => d.GetProperty("id").GetGuid())));
        Assert.Equal(205, digests.Select(d => d.GetProperty("id").GetGuid()).Distinct().Count());
        Assert.All(digests, d =>
        {
            Assert.Equal(_owner, d.GetProperty("userId").GetGuid());
            Assert.Single(d.GetProperty("recommendations").EnumerateArray());
            Assert.True(d.GetProperty("recommendations")[0].GetProperty("isDeleted").GetBoolean());
        });
        Assert.Single(digests.Select(d => d.GetProperty("createdAt").GetString()).Distinct());
    }

    private async Task CaptureBothAsync()
    {
        foreach (var id in new[] { _owner, _other })
        {
            await using var scope = _services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            _ = services.GetRequiredService<IUnsubscribeTokens>().Create(id);
            await using (var rows = await services.GetRequiredService<ILocalDigestCaptureStore>().BeginAsync(id, _week, default)) { }
            LocalCaptureResult? captured = null;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var state = await services.GetRequiredService<IDigestGenerationStore>().ReadStorageAsync();
                await services.GetRequiredService<LocalGenerationStorage>().ReconcileAsync(state);
                captured = await services.GetRequiredService<ISender>().Send(new CaptureLocalDigestCommand(id, _week));
                if (captured.Status == "local_captured") break;
                Assert.Equal("database_inventory_changed", captured.Reason);
                var db = services.GetRequiredService<DataContext>();
                var digest = await db.WeeklyDigests.SingleAsync(d => d.UserId == id && d.Week == _week);
                Assert.Equal(DigestStatus.Pending, digest.Status);
                Assert.False(File.Exists(Path.Combine(_email.SinkRoot!, $"{digest.Id:N}.eml.enc")));
            }
            Assert.Equal("local_captured", captured!.Status);
        }
    }

    [PostgresFact]
    public async Task Delete_AllOwnedTablesAndEncryptedCopies_RevokesEveryCapability_PreservesOtherAccountsAndSharedData()
    {
        await CaptureBothAsync();
        await using var scope = _services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var otherDigest = await db.WeeklyDigests.SingleAsync(d => d.UserId == _other);
        var otherFile = Path.Combine(_email.SinkRoot!, $"{otherDigest.Id:N}.eml.enc");
        var otherBytes = await File.ReadAllBytesAsync(otherFile);
        var capability = scope.ServiceProvider.GetRequiredService<IUnsubscribeTokens>().Create(_owner);
        var keyCount = await db.DataProtectionKeys.CountAsync();
        await using var app = App(); using var client = AuthenticatedClient(app, _owner);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/subscribers/me")).StatusCode);
        await AssertOwnedRowsAsync(db, _owner, 0);
        await AssertOwnedRowsAsync(db, _other, 1);
        Assert.Equal(2, await db.Tracks.CountAsync()); Assert.Equal(2, await db.Artists.CountAsync()); Assert.Equal(2, await db.Albums.CountAsync());
        Assert.Equal(keyCount, await db.DataProtectionKeys.CountAsync()); Assert.Single(await db.Roles.ToListAsync()); Assert.Single(await db.RoleClaims.ToListAsync());
        Assert.Equal(otherBytes, await File.ReadAllBytesAsync(otherFile)); Assert.Single(Directory.GetFiles(_email.SinkRoot!));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/export/my-data")).StatusCode);
        Assert.False(await scope.ServiceProvider.GetRequiredService<IAuthService>().ValidateRefreshTokenAsync(_owner, "refresh-fixture-owner"));
        Assert.True(scope.ServiceProvider.GetRequiredService<IUnsubscribeTokens>().TryRead(capability, out var tokenOwner));
        await scope.ServiceProvider.GetRequiredService<IUnsubscribeStore>().UnsubscribeAsync(tokenOwner, DateTime.UtcNow, default);
        Assert.Null((await db.Users.SingleAsync(u => u.Id == _other)).EmailUnsubscribedAtUtc);
        Assert.True((await scope.ServiceProvider.GetRequiredService<ILocalEmailArchive>().DeleteAsync(_owner, default)).Complete);
    }

    [PostgresFact]
    public async Task Delete_CredentialFailureRollsBackOwnedRowsAndKeepsCopies()
    {
        await CaptureBothAsync();
        var before = Directory.GetFiles(_email.SinkRoot!).ToDictionary(path => Path.GetFileName(path)!, File.ReadAllBytes);
        await using var app = App(failCredentials: true); using var client = AuthenticatedClient(app, _owner);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync("/api/subscribers/me")).StatusCode);
        await using var scope = _services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        await AssertOwnedRowsAsync(db, _owner, 1);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/export/my-data")).StatusCode);
        Assert.True(await scope.ServiceProvider.GetRequiredService<IAuthService>().ValidateRefreshTokenAsync(_owner, "refresh-fixture-owner"));
        Assert.Equal(before.Count, Directory.GetFiles(_email.SinkRoot!).Length);
        foreach (var file in Directory.GetFiles(_email.SinkRoot!)) Assert.Equal(before[Path.GetFileName(file)], await File.ReadAllBytesAsync(file));
    }

    [PostgresFact]
    public async Task Delete_CopyIntegrityFailureRevokesAccessAndReportsIncompleteCleanup()
    {
        await CaptureBothAsync();
        await using var scope = _services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var other = await db.WeeklyDigests.SingleAsync(d => d.UserId == _other);
        var otherFile = Path.Combine(_email.SinkRoot!, $"{other.Id:N}.eml.enc");
        var bytes = await File.ReadAllBytesAsync(otherFile);
        var corrupt = Path.Combine(_email.SinkRoot!, "fixture-corrupt.partial");
        await File.WriteAllBytesAsync(corrupt, [1, 2, 3]);
        await using var app = App(); using var client = AuthenticatedClient(app, _owner);
        using var response = await client.DeleteAsync("/api/subscribers/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(result.RootElement.GetProperty("accountDeleted").GetBoolean());
        Assert.False(result.RootElement.GetProperty("localCopiesDeleted").GetBoolean());
        Assert.Contains("copy_cleanup_integrity_unavailable", result.RootElement.GetProperty("issues").EnumerateArray().Select(v => v.GetString()));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        await AssertOwnedRowsAsync(db, _owner, 0);
        Assert.False(await scope.ServiceProvider.GetRequiredService<IAuthService>().ValidateRefreshTokenAsync(_owner, "refresh-fixture-owner"));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(otherFile));
        Assert.True(File.Exists(corrupt)); File.Delete(corrupt); // Only this fabricated fixture is removed by the operator simulation.
        Assert.True((await scope.ServiceProvider.GetRequiredService<ILocalEmailArchive>().DeleteAsync(_owner, default)).Complete);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(otherFile)); Assert.Single(Directory.GetFiles(_email.SinkRoot!));
    }

    [PostgresFact]
    public async Task Export_LaterPageFailureReturnsProblemWithoutPartialExport()
    {
        await using var app = App(failExport: true);
        using var client = AuthenticatedClient(app, _owner);
        using var response = await client.GetAsync("/api/export/my-data");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(500, body.RootElement.GetProperty("status").GetInt32());
        Assert.False(body.RootElement.TryGetProperty("exportVersion", out _));
        Assert.False(body.RootElement.TryGetProperty("subscriber", out _));
        Assert.False(body.RootElement.TryGetProperty("digests", out _));
    }

    private static async Task AssertOwnedRowsAsync(DataContext db, Guid id, int count)
    {
        Assert.Equal(count, await db.Users.IgnoreQueryFilters().CountAsync(u => u.Id == id));
        Assert.Equal(count, await db.ApplicationUsers.CountAsync(u => u.Id == id));
        Assert.Equal(count, await db.UserMusicConnections.IgnoreQueryFilters().CountAsync(c => c.UserId == id));
        Assert.Equal(count, await db.TasteSignals.IgnoreQueryFilters().CountAsync(s => s.UserId == id));
        Assert.Equal(count, await db.WeeklyDigests.IgnoreQueryFilters().CountAsync(d => d.UserId == id));
        Assert.Equal(count, await db.WeeklyRecommendations.IgnoreQueryFilters().CountAsync(r => r.UserId == id));
        Assert.Equal(count, await db.UserClaims.CountAsync(c => c.UserId == id)); Assert.Equal(count, await db.UserLogins.CountAsync(l => l.UserId == id));
        Assert.Equal(count, await db.UserRoles.CountAsync(r => r.UserId == id)); Assert.Equal(count, await db.UserTokens.CountAsync(t => t.UserId == id));
    }

    public async Task DisposeAsync()
    {
        if (_services is not null) await _services.DisposeAsync();
        if (_admin.Length > 0)
        {
            await using var connection = new NpgsqlConnection(_admin); await connection.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", connection);
            await drop.ExecuteNonQueryAsync();
        }
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class TestApp(IConfiguration configuration, LocalEmailOptions email, GenerationStorageOptions storage,
        bool failCredentials, bool failExport) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            foreach (var value in configuration.AsEnumerable()) builder.UseSetting(value.Key, value.Value);
            builder.ConfigureAppConfiguration((_, config) => config.AddConfiguration(configuration));
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(email).AddSingleton(storage);
                if (failExport)
                {
                    var digests = Substitute.For<IWeeklyDigestRepository>();
                    digests.GetExportPageAsync(Arg.Any<Guid>(), Arg.Any<DateTime?>(), Arg.Any<Guid?>(), 100, Arg.Any<CancellationToken>())
                        .Returns(call => call.ArgAt<DateTime?>(1) is null
                            ? Task.FromResult<IReadOnlyList<WeeklyDigest>>(Enumerable.Range(0, 100)
                                .Select(_ => new WeeklyDigest(call.ArgAt<Guid>(0), new IsoWeek(2026, 42), DateTime.UtcNow, DateTime.UtcNow.AddDays(7))).ToArray())
                            : Task.FromException<IReadOnlyList<WeeklyDigest>>(new IOException("injected later-page failure")));
                    services.AddSingleton(digests);
                }
                if (failCredentials)
                {
                    var auth = Substitute.For<IAuthService>();
                    auth.DeleteUserAsync(Arg.Any<Guid>()).Returns(false);
                    services.AddSingleton(auth);
                }
            });
        }
    }
}
