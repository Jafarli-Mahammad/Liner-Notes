using System.Globalization;
using System.Security.Cryptography;
using System.Xml.Linq;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed partial class DiscoveryCoordinationPostgresTests
{
    [LocalPostgresMigrationFact]
    public async Task AnInitializedProtocolRejectsAnUncoordinatedManagedMutationAtCommit()
    {
        await EnableDurableProtocolAsync();
        await using var scope = services.CreateAsyncScope();
        var before = await scope.ServiceProvider.GetRequiredService<IDigestGenerationStore>().ReadStorageAsync();
        await using var connection = new NpgsqlConnection(this.connection);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var mutate = new NpgsqlCommand("UPDATE \"ManagedStorageCommit\" SET \"OwnedChangesJson\"='[]' WHERE \"Id\"=1", connection, transaction);
        await mutate.ExecuteNonQueryAsync();
        var failure = await Assert.ThrowsAsync<PostgresException>(() => transaction.CommitAsync());
        Assert.Equal("23514", failure.SqlState);
        Assert.Equal("managed_storage_commit_witness", failure.ConstraintName);
        Assert.Equal(before.Revision, (await scope.ServiceProvider.GetRequiredService<IDigestGenerationStore>().ReadStorageAsync()).Revision);
        await scope.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
    }

    [LocalPostgresMigrationFact]
    public async Task StandardDataProtectionCreationAndExpiredKeyRotationRemainAccountedAndOldTokensDecrypt()
    {
        await EnableDurableProtocolAsync();
        string original = services.GetRequiredService<IUnsubscribeTokenProtection>().Protect("fixture-original");
        int count;
        await WriteAsync(async (provider, ct) =>
        {
            var db = provider.GetRequiredService<DataContext>();
            var keys = await db.DataProtectionKeys.AsTracking().ToArrayAsync(ct);
            foreach (var key in keys)
            {
                var xml = XElement.Parse(key.Xml!);
                foreach (var (name, days) in new[] { ("creationDate", -90), ("activationDate", -89), ("expirationDate", -1) })
                    xml.Element(name)!.Value = DateTimeOffset.UtcNow.AddDays(days).ToString("O", CultureInfo.InvariantCulture);
                key.Xml = xml.ToString(SaveOptions.DisableFormatting);
            }
            await db.SaveChangesAsync(ct);
            return true;
        });
        await using (var check = services.CreateAsyncScope())
            count = await check.ServiceProvider.GetRequiredService<DataContext>().DataProtectionKeys.CountAsync();
        using var restarted = new LocalUnsubscribeProtection(connection, services.GetRequiredService<IServiceScopeFactory>());
        string fresh = restarted.Protect("fixture-fresh");
        Assert.Equal("fixture-original", restarted.Unprotect(original));
        Assert.Equal("fixture-fresh", restarted.Unprotect(fresh));
        await using var verified = services.CreateAsyncScope();
        Assert.True(await verified.ServiceProvider.GetRequiredService<DataContext>().DataProtectionKeys.CountAsync() > count);
        await verified.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
        await WriteAsync((_, _) => Task.FromResult(true), 0);
    }

    [LocalPostgresMigrationFact]
    public async Task FirstKeyCreationInsideAdmissionFailsWithoutRecursivelyWaitingAndCanRetryOutside()
    {
        await EnableDurableProtocolAsync();
        var failure = await Record.ExceptionAsync(() => WriteAsync((provider, _) =>
            Task.FromResult(provider.GetRequiredService<IUnsubscribeTokenProtection>().Protect("fixture-recursion"))).WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.NotNull(failure);
        Assert.IsNotType<TimeoutException>(failure);
        await using (var check = services.CreateAsyncScope())
            Assert.Empty(await check.ServiceProvider.GetRequiredService<DataContext>().DataProtectionKeys.ToArrayAsync());
        string token = services.GetRequiredService<IUnsubscribeTokenProtection>().Protect("fixture-after-rollback");
        Assert.Equal("fixture-after-rollback", services.GetRequiredService<IUnsubscribeTokenProtection>().Unprotect(token));
        await WriteAsync((_, _) => Task.FromResult(true), 0);
    }
}
