using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.DataAccess.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace LinerNotes.Presentation.Tests;

public sealed class GuestDiscoveryApiTests : IAsyncLifetime
{
    [PostgresFact]
    public async Task DiscoverySubmissionUsesCsrfAndPrivateStatusWithSafeCooldownAndConflictResponses()
    {
        using var client = _app.CreateClient();
        var overview = await client.GetFromJsonAsync<JsonElement>("/api/discovery");
        Assert.Equal(JsonValueKind.Null, overview.GetProperty("page").ValueKind);
        Assert.Contains(overview.GetProperty("styles").EnumerateArray(), x => x.GetString() == "jazz");
        string csrf = (await client.GetFromJsonAsync<JsonElement>("/api/discovery/session")).GetProperty("csrfToken").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Mutation(HttpMethod.Post, "/api/discovery/session", csrf))).StatusCode);
        _app.Services.GetRequiredService<LinerNotes.Application.Features.Discovery.DiscoveryRuntimeOptions>().RunJobs = true;
        object Body(string key, string tag, long revision = 0) => new { revision, requestKey = key, selection = new { catalogRecordIds = Array.Empty<Guid>(), tags = new[] { tag } } };
        using (var missing = Mutation(HttpMethod.Post, "/api/discovery/batches", null)) {
            missing.Content = JsonContent.Create(Body("initial", "jazz"));
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(missing)).StatusCode);
        }
        async Task<HttpResponseMessage> Submit(object body) {
            using var request = Mutation(HttpMethod.Post, "/api/discovery/batches", csrf);
            request.Content = JsonContent.Create(body); return await client.SendAsync(request);
        }
        using var accepted = await Submit(Body("initial", "jazz"));
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        var job = await accepted.Content.ReadFromJsonAsync<JsonElement>();
        Guid id = job.GetProperty("id").GetGuid();
        Assert.DoesNotContain("lease", job.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("payload", job.ToString(), StringComparison.OrdinalIgnoreCase);
        using var replay = await Submit(Body("initial", "JAZZ"));
        Assert.Equal(id, (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        using var status = await client.GetAsync($"/api/discovery/jobs/{id}");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode); Assert.True(status.Headers.CacheControl!.NoStore);
        using var cooldown = await Submit(new { revision = 1, requestKey = "more" });
        Assert.Equal(HttpStatusCode.TooManyRequests, cooldown.StatusCode);
        Assert.True(cooldown.Headers.RetryAfter!.Delta > TimeSpan.Zero);
        using var conflict = await Submit(Body("initial", "ambient"));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var stranger = _app.CreateClient();
        string foreignCsrf = (await stranger.GetFromJsonAsync<JsonElement>("/api/discovery/session")).GetProperty("csrfToken").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await stranger.SendAsync(Mutation(HttpMethod.Post, "/api/discovery/session", foreignCsrf))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/discovery/jobs/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/discovery/batches/{Guid.NewGuid()}")).StatusCode);
    }
    private readonly string _database = "liner_guest_api_" + Guid.NewGuid().ToString("N");
    private string _admin = "";
    private TestApp _app = null!;

    public async Task InitializeAsync()
    {
        _admin = Environment.GetEnvironmentVariable("LINER_PHASE1_POSTGRES") ?? "";
        if (_admin.Length == 0) return;
        var settings = new NpgsqlConnectionStringBuilder(_admin);
        Assert.Contains(settings.Host, new[] { "localhost", "127.0.0.1", "::1" });
        await using (var db = new NpgsqlConnection(_admin))
        {
            await db.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_database}\"", db);
            await create.ExecuteNonQueryAsync();
        }
        settings.Database = _database;
        _app = new(settings.ConnectionString);
        await using var scope = _app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_app is not null) await _app.DisposeAsync();
        if (_admin.Length == 0) return;
        await using var db = new NpgsqlConnection(_admin);
        await db.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", db);
        await drop.ExecuteNonQueryAsync();
    }

    [PostgresFact]
    public async Task BootstrapDoesNotAllocateAndCookieMutationsRequireOriginAndCsrf()
    {
        using var client = _app.CreateClient();
        var bootstrap = await client.GetFromJsonAsync<JsonElement>("/api/discovery/session");
        Assert.Equal(JsonValueKind.Null, bootstrap.GetProperty("profile").ValueKind);
        var csrf = bootstrap.GetProperty("csrfToken").GetString()!;
        await using (var scope = _app.Services.CreateAsyncScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<DataContext>().ListenerProfiles.AnyAsync());
        using var noToken = await client.SendAsync(Mutation(HttpMethod.Post, "/api/discovery/session", null));
        Assert.Equal(HttpStatusCode.Forbidden, noToken.StatusCode);
        using var foreign = Mutation(HttpMethod.Post, "/api/discovery/session", csrf);
        foreign.Headers.Remove("Origin");
        foreign.Headers.Add("Origin", "https://untrusted.example");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(foreign)).StatusCode);
        using var started = await client.SendAsync(Mutation(HttpMethod.Post, "/api/discovery/session", csrf));
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        Assert.Contains(started.Headers.GetValues("Set-Cookie"), c => c.StartsWith("liner.guest=") && c.Contains("httponly", StringComparison.OrdinalIgnoreCase) && c.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase));
        var profileId = (await started.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var exported = await client.GetFromJsonAsync<JsonElement>("/api/discovery/session/data");
        Assert.Equal(profileId, exported.GetProperty("discovery").GetProperty("profile").GetProperty("id").GetGuid());
        Assert.DoesNotContain("credentialHash", exported.ToString(), StringComparison.OrdinalIgnoreCase);
        using (var lostCookie = _app.CreateClient())
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await lostCookie.GetAsync("/api/discovery/session/data")).StatusCode);
            lostCookie.DefaultRequestHeaders.Add("Cookie", "liner.guest=" + profileId.ToString("N") + profileId.ToString("N"));
            Assert.Equal(HttpStatusCode.Unauthorized, (await lostCookie.GetAsync("/api/discovery/session/data")).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Mutation(HttpMethod.Delete, "/api/discovery/session", "forged"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(Mutation(HttpMethod.Delete, "/api/discovery/session", csrf))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/discovery/session/data")).StatusCode);
    }

    [PostgresFact]
    public async Task RegistrationKeepsTheGuestProfileAndRevokesItsCookie()
    {
        using var client = _app.CreateClient();
        var bootstrap = await client.GetFromJsonAsync<JsonElement>("/api/discovery/session");
        var csrf = bootstrap.GetProperty("csrfToken").GetString()!;
        var start = await client.SendAsync(Mutation(HttpMethod.Post, "/api/discovery/session", csrf));
        var profileId = (await start.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var register = Mutation(HttpMethod.Post, "/api/auth/register", csrf);
        register.Content = JsonContent.Create(new { userName = "guest-registration", email = "guest-registration@example.test", password = "LocalTestPassword123!" });
        using var registered = await client.SendAsync(register);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var response = await registered.Content.ReadFromJsonAsync<JsonElement>();
        var accountId = response.GetProperty("user").GetProperty("id").GetGuid();
        Assert.NotEqual(profileId, accountId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", response.GetProperty("accessToken").GetString());
        var restored = await client.GetFromJsonAsync<JsonElement>("/api/discovery/session");
        Assert.Equal(profileId, restored.GetProperty("profile").GetProperty("id").GetGuid());
        Assert.False(restored.GetProperty("profile").GetProperty("isGuest").GetBoolean());
        await using var scope = _app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        Assert.Equal(accountId, (await db.ListenerProfiles.SingleAsync()).UserId);
        Assert.False(await db.GuestSessions.AnyAsync());
        Assert.False((await db.Users.SingleAsync(u => u.Id == accountId)).WeeklyEmailOptIn);
        Assert.False((await db.ApplicationUsers.SingleAsync(u => u.Id == accountId)).EmailConfirmed);
    }

    [PostgresFact]
    public async Task CatalogSearchIsPublicAndLookupStatusIsPrivateWhileSubmissionIsDisabled()
    {
        using var client = _app.CreateClient();
        using var search = await client.GetAsync("/api/catalog/search?query=missing&kind=Album");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        Assert.Empty((await search.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray());
        await using (var empty = _app.Services.CreateAsyncScope())
            Assert.False(await empty.ServiceProvider.GetRequiredService<DataContext>().ListenerProfiles.AnyAsync());
        var bootstrap = await client.GetFromJsonAsync<JsonElement>("/api/discovery/session");
        var csrf = bootstrap.GetProperty("csrfToken").GetString()!;
        using var started = await client.SendAsync(Mutation(HttpMethod.Post, "/api/discovery/session", csrf));
        var profileId = (await started.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Guid ownJob, foreignJob;
        await using (var scope = _app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var other = LinerNotes.Domain.Discovery.ListenerProfile.Guest(DateTimeOffset.UtcNow, TimeSpan.FromDays(7));
            db.ListenerProfiles.Add(other);
            var owned = new LinerNotes.Domain.Discovery.DiscoveryJob(profileId, "lookup-one", new string('a', 64),
                LinerNotes.Domain.Discovery.DiscoveryJobKind.Lookup, 0, "{\"query\":\"transient private query\"}", DateTimeOffset.UtcNow);
            var foreign = new LinerNotes.Domain.Discovery.DiscoveryJob(other.Id, "lookup-other", new string('b', 64),
                LinerNotes.Domain.Discovery.DiscoveryJobKind.Lookup, 0, "{}", DateTimeOffset.UtcNow);
            db.DiscoveryJobs.AddRange(owned, foreign);
            await db.SaveChangesAsync(); ownJob = owned.Id; foreignJob = foreign.Id;
        }
        using var own = await client.GetAsync($"/api/catalog/lookups/{ownJob}");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.DoesNotContain("transient private query", await own.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/catalog/lookups/{foreignJob}")).StatusCode);
        using var submit = Mutation(HttpMethod.Post, "/api/catalog/lookup", csrf);
        submit.Content = JsonContent.Create(new { query = "Any missing favorite", kind = "Album", requestKey = "explicit-lookup" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.SendAsync(submit)).StatusCode);
        await using var check = _app.Services.CreateAsyncScope();
        Assert.Equal(2, await check.ServiceProvider.GetRequiredService<DataContext>().DiscoveryJobs.CountAsync());
    }

    private static HttpRequestMessage Mutation(HttpMethod method, string path, string? csrf)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Origin", "http://localhost");
        if (csrf is not null) request.Headers.Add("X-CSRF-TOKEN", csrf);
        return request;
    }

    // Mechanics-only fixture. Production remains fail-closed until C7 installs accounting.
    private sealed class TransactionalTestAdmission(IUnitOfWork uow) : IDiscoveryWriteCoordinator
    {
        public Task<T> ReclaimAsync<T>(Func<CancellationToken, Task<T>> delete, CancellationToken ct) => ExecuteAsync(0, delete, ct);
        public Task<T> ExecuteAsync<T>(long estimatedBytes, Func<CancellationToken, Task<T>> write, CancellationToken ct) =>
            uow.ExecuteInTransactionAsync(write, ct);
    }

    private sealed class TestApp(string connection) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connection,
                ["Jwt:SecretKey"] = "guest-api-fixture-signing-key-only-not-a-deployment-secret",
                ["GuestDiscovery:Enabled"] = "true", ["GuestDiscovery:PublicOrigin"] = "http://localhost",
                ["LastFm:Mode"] = "FixtureOnly", ["LocalEmail:Enabled"] = "false"
            }).Build();
            foreach (var pair in settings.AsEnumerable()) builder.UseSetting(pair.Key, pair.Value);
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddConfiguration(settings));
            builder.ConfigureServices(services => services.AddScoped<IDiscoveryWriteCoordinator, TransactionalTestAdmission>());
        }
    }
}
