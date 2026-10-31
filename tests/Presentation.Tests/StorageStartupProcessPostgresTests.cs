using System.Diagnostics;
using LinerNotes.DataAccess.Persistence;
using LinerNotes.Presentation.Controllers;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LinerNotes.Presentation.Tests;

public sealed partial class CoordinatedAuthenticationPostgresTests
{
    [PostgresFact]
    public Task MissingStorageMigrationsExitCleanlyWithoutStartingOrAborting() =>
        VerifyStartupStopsAsync(true, "storage_schema_upgrade_required");

    [PostgresFact]
    public Task UninitializedStorageWitnessExitsCleanlyWithoutStartingOrAborting() =>
        VerifyStartupStopsAsync(false, "storage_commit_witness_uninitialized");

    private async Task VerifyStartupStopsAsync(bool oldSchema, string reason)
    {
        await using (var scope = services.CreateAsyncScope())
        {
            var migrations = scope.ServiceProvider.GetRequiredService<AppDbContext>().GetService<IMigrator>();
            await migrations.MigrateAsync("20261019084129_StoreRecordingReleaseHints");
            if (!oldSchema) await migrations.MigrateAsync();
        }

        string dotnet = Environment.GetEnvironmentVariable("DOTNET_ROOT") is { } sdkRoot ? Path.Combine(sdkRoot, "dotnet") : "dotnet";
        var start = new ProcessStartInfo(dotnet)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string argument in new[] { typeof(AuthController).Assembly.Location,
            "--contentRoot", root, "--urls", "http://127.0.0.1:0", "--LocalLive:Enabled", "false" })
            start.ArgumentList.Add(argument);
        foreach (var setting in new Dictionary<string, string>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development", ["DOTNET_ENVIRONMENT"] = "Development",
            ["ConnectionStrings__DefaultConnection"] = connection,
            ["Jwt__SecretKey"] = "startup-process-fixture-only-signing-key-123456789",
            ["Generation__Origin"] = "Synthetic", ["LastFm__Mode"] = "FixtureOnly",
            ["DiscoveryRuntime__RunJobs"] = "false", ["DiscoveryRuntime__EnforceStorageAdmission"] = "true",
            ["GenerationStorage__InventoryPath"] = Path.Combine(root, "inventory.json"),
            ["ForwardedHeaders__KnownProxies__0"] = "127.0.0.1"
        }) start.Environment[setting.Key] = setting.Value;
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45)); }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } }
        string output = await stdout + await stderr;
        Assert.Equal(2, process.ExitCode);
        Assert.Contains("Managed storage startup stopped: " + reason, output);
        Assert.Contains("STORAGE_AUTH_REDESIGN_IMPLEMENTATION.md", output);
        Assert.DoesNotContain("Unhandled exception.", output);
        Assert.DoesNotContain("Now listening on", output);
    }
}
