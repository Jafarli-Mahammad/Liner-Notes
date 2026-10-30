using System.Security.Cryptography;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Domain.Enums;
using LinerNotes.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed partial class Phase8ScheduledWorkerPostgresTests
{
    [ScheduledPostgresTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ManagedWorkerTerminationAtPublicationBoundariesRecoversWithoutReconciliation(int targetPhase)
    {
        await EnableManagedCaptureAsync();
        await using var barrier = new NpgsqlConnection(_connection);
        await barrier.OpenAsync();
        await using (var hold = new NpgsqlCommand("SELECT pg_advisory_lock(1280200761)", barrier)) await hold.ExecuteNonQueryAsync();
        await using (var install = new NpgsqlCommand($"""
            CREATE FUNCTION phase8_artifact_barrier() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN PERFORM pg_advisory_xact_lock(1280200761); RETURN NEW; END; $$;
            CREATE TRIGGER phase8_artifact_barrier BEFORE {(targetPhase == 0 ? "INSERT" : "UPDATE OF \"Phase\"")} ON "ArtifactPublications"
              FOR EACH ROW WHEN (NEW."Phase" = {targetPhase}) EXECUTE FUNCTION phase8_artifact_barrier();
            """, barrier)) await install.ExecuteNonQueryAsync();
        await ReconcileAsync(); // Explicitly establish the test-only DDL before starting either host.
        using var first = StartWorker();
        var firstOutput = first.StandardOutput.ReadToEndAsync();
        var firstError = first.StandardError.ReadToEndAsync();
        byte[]? publishedHash = null;
        try
        {
            int backend = await WaitForBarrierAsync(first);
            string? final = Directory.GetFiles(_email.SinkRoot!, "*.eml.enc").SingleOrDefault();
            if (targetPhase == 3)
            {
                Assert.NotNull(final);
                publishedHash = SHA256.HashData(await File.ReadAllBytesAsync(final));
            }
            first.Kill(entireProcessTree: true);
            await first.WaitForExitAsync();
            await using (var terminate = new NpgsqlCommand("SELECT pg_terminate_backend(@pid,5000)", barrier))
            { terminate.Parameters.AddWithValue("pid", backend); await terminate.ExecuteNonQueryAsync(); }
            await using (var release = new NpgsqlCommand("SELECT pg_advisory_unlock(1280200761)", barrier)) await release.ExecuteNonQueryAsync();
            // Keep the same DDL/fingerprint. No inventory reconciliation is allowed on restart.
            using var restarted = StartWorker();
            var output = restarted.StandardOutput.ReadToEndAsync();
            var error = restarted.StandardError.ReadToEndAsync();
            try
            {
                await restarted.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45));
                Assert.True(restarted.ExitCode == 0, $"Restarted worker failed: {await output}; {await error}");
                Assert.Empty(await error);
            }
            finally
            {
                if (!restarted.HasExited) { restarted.Kill(entireProcessTree: true); await restarted.WaitForExitAsync(); }
            }
            string complete = Assert.Single(Directory.GetFiles(_email.SinkRoot!, "*.eml.enc"));
            if (publishedHash is not null) Assert.Equal(publishedHash, SHA256.HashData(await File.ReadAllBytesAsync(complete)));
            Assert.Empty(Directory.GetFiles(_email.SinkRoot!, "*.staged"));
            await using var check = _services.CreateAsyncScope();
            Assert.Equal(DigestStatus.LocalCaptured, (await check.ServiceProvider.GetRequiredService<DataContext>().WeeklyDigests.SingleAsync()).Status);
            await check.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
        }
        finally
        {
            if (!first.HasExited) { first.Kill(entireProcessTree: true); await first.WaitForExitAsync(); }
            await firstOutput; await firstError;
            await using var drop = new NpgsqlCommand("DROP TRIGGER phase8_artifact_barrier ON \"ArtifactPublications\"; DROP FUNCTION phase8_artifact_barrier();", barrier);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
