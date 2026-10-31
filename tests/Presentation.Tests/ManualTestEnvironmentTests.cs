using System.Net;
using System.Net.Http.Json;
using LinerNotes.Presentation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LinerNotes.Presentation.Tests;

public sealed class ManualTestEnvironmentTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/app.js")]
    [InlineData("/discovery.js")]
    [InlineData("/styles.css")]
    [InlineData("/discoveries")]
    public async Task FrontendAssets_RequireRevalidation(string path)
    {
        await using var app = new ManualTestApp("Development");
        using var client = app.CreateClient();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoCache);
    }

    [Fact]
    public async Task Development_ServesManualTestPageAndStatus()
    {
        await using var app = new ManualTestApp("Development");
        using var client = app.CreateClient();

        var page = await client.GetAsync("/manual-test");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("Manual QA", html);
        Assert.Contains("DEVELOPMENT ONLY", html);
        Assert.Contains("/manual-test/manual-test.css", html);
        Assert.Contains("/manual-test/manual-test.js", html);

        var status = await client.GetAsync("/api/dev/manual-test/status");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
    }

    [Fact]
    public async Task Production_HidesManualTestPageAndEndpoints()
    {
        await using var app = new ManualTestApp("Production");
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        foreach (var path in new[]
        {
            "/manual-test",
            "/manual-test/manual-test.js",
            "/api/dev/manual-test/status",
            "/api/dev/manual-test/storage/reconcile",
            "/api/dev/manual-test/digests"
        })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("X-Forwarded-Proto", "https");
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    public async Task Development_ReconcileAndGenerateRequireAuthentication()
    {
        await using var app = new ManualTestApp("Development");
        using var client = app.CreateClient();

        var reconcile = await client.PostAsync("/api/dev/manual-test/storage/reconcile", content: null);
        var generate = await client.PostAsJsonAsync("/api/dev/manual-test/digests", new { week = "2026-W41" });

        Assert.Equal(HttpStatusCode.Unauthorized, reconcile.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, generate.StatusCode);
    }

    [Fact]
    public async Task Status_DoesNotExposeSecretsOrFilesystemPaths()
    {
        await using var app = new ManualTestApp("Development");
        using var client = app.CreateClient();

        var response = await client.GetAsync("/api/dev/manual-test/status");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        foreach (var forbidden in new[]
        {
            "SecretKey", "AccessToken", "RefreshToken", "Sha256", "InventoryPath",
            "LeasePath", "ArtifactRoots", "ConnectionString", "Password"
        })
            Assert.DoesNotContain(forbidden, body, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ManualTestApp(string environment) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Port=55439;Database=qa_tests;Username=qa;Password=not-a-real-secret",
                ["Jwt:SecretKey"] = "manual-qa-test-signing-key-only-never-deploy-1234567890",
                ["Jwt:Issuer"] = "ManualQaTests",
                ["Jwt:Audience"] = "ManualQaTests",
                ["ForwardedHeaders:KnownProxies:0"] = "127.0.0.1",
                ["ForwardedHeaders:KnownProxies:1"] = "::1",
                ["Generation:Origin"] = "Synthetic",
                ["Generation:ApprovedRecordingManifestSha256"] = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                ["GenerationStorage:InventoryPath"] = "/tmp/manual-qa-inventory.json",
                ["GenerationStorage:LeasePath"] = "/tmp/manual-qa-lease.lock",
                ["GenerationStorage:DatabaseOverheadBytesPerPick"] = "1000000"
            };
            foreach (var setting in settings)
                builder.UseSetting(setting.Key, setting.Value);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
            // These tests exercise HTTP routing and cache headers without a database.
            // Real initialized-witness startup and downgrade checks have PostgreSQL coverage.
            builder.ConfigureServices(services => services.Replace(
            ServiceDescriptor.Scoped<IManagedStorageCommitStore, UninitializedCommitStore>()));
        }
    }
    private sealed class UninitializedCommitStore : IManagedStorageCommitStore
    {
        public Task<ManagedStorageCommit?> ReadAsync(CancellationToken ct) => Task.FromResult<ManagedStorageCommit?>(null);
        public Task<ManagedStorageCommit> RecordAsync(Guid id, StoragePublicationPlan plan, string fingerprint, CancellationToken ct)
        => throw new InvalidOperationException("HTTP fixture must not mutate managed storage.");
        public Task<GenerationStorageState> EstablishAsync(Guid id, StoragePublicationPlan plan, CancellationToken ct)
        => throw new InvalidOperationException("HTTP fixture must not establish managed storage.");
    }
}
