using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Features.Subscribers.Commands.DeleteUserAccount;
using LinerNotes.DataAccess.DataContexts;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed partial class Phase8ScheduledWorkerPostgresTests
{
    private async Task DeleteManagedAccountDatabaseAsync()
    {
        await using var scope = _services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        await provider.GetRequiredService<IDiscoveryWriteCoordinator>().ReclaimAsync(async ct =>
        {
            Assert.True(await provider.GetRequiredService<ISender>().Send(new DeleteUserAccountCommand(_user), ct));
            var db = provider.GetRequiredService<DataContext>();
            await db.UserTokens.Where(token => token.UserId == _user).ExecuteDeleteAsync(ct);
            Assert.Equal(1, await db.ApplicationUsers.IgnoreQueryFilters().Where(user => user.Id == _user).ExecuteDeleteAsync(ct));
            return true;
        }, default);
    }

    [LocalPostgresMigrationFact]
    public async Task RestartRecoveryFinishesPrivacyCleanupInterruptedAfterTheAccountCommit()
    {
        await EnableManagedCaptureAsync();
        Assert.Equal("advanced", Assert.Single((await TickAsync()).Jobs).Status);
        await DeleteManagedAccountDatabaseAsync();
        Assert.Single(Directory.GetFiles(_email.SinkRoot!));
        // A new startup scope has no controller state from the interrupted deletion.
        var startup = Assert.Single(_services.GetServices<Microsoft.Extensions.Hosting.IHostedService>(),
            service => service.GetType().Name == "StorageProtocolStartup");
        await startup.StartAsync(default);
        await using var scope = _services.CreateAsyncScope();
        Assert.Empty(Directory.GetFiles(_email.SinkRoot!));
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<IArtifactPublicationStore>().GetOwnerPageAsync(_user, null, 100, default));
        await scope.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
    }

    [LocalPostgresMigrationFact]
    public async Task ManagedPrivacyCleanupAfterAccountCommitErasesFilesAndOwnedJournalRows()
    {
        await EnableManagedCaptureAsync();
        Assert.Equal("advanced", Assert.Single((await TickAsync()).Jobs).Status);
        await using var scope = _services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        await using var ownership = await provider.GetRequiredService<IAccountEmailLease>().AcquireAsync(_user, default);
        await DeleteManagedAccountDatabaseAsync();
        var cleanup = await provider.GetRequiredService<ILocalEmailArchive>().DeleteAsync(_user, default);
        Assert.True(cleanup.Complete, string.Join(",", cleanup.Issues));
        Assert.Empty(Directory.GetFiles(_email.SinkRoot!));
        Assert.Empty(await provider.GetRequiredService<IArtifactPublicationStore>().GetOwnerPageAsync(_user, null, 100, default));
        Assert.True(await provider.GetRequiredService<DataContext>().ApplicationUsers.AnyAsync(user => user.Id == _other));
        await provider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
        Assert.True((await provider.GetRequiredService<ILocalEmailArchive>().DeleteAsync(_user, default)).Complete);
    }

    [ScheduledPostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManagedPrivacyCleanupCancelsPreparedAndUncommittedPublishedCopies(bool rename)
    {
        await EnableManagedCaptureAsync();
        await using var scope = _services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var publisher = provider.GetRequiredService<IRecoverableArtifactPublisher>();
        var operation = await publisher.StageAsync("copies", _email.SinkRoot!, "cancel-fixture.eml.enc", "private cancellation fixture"u8.ToArray(), default, _user);
        if (rename) operation = await publisher.PublishAsync(operation, default);
        var cleanup = await provider.GetRequiredService<ILocalEmailArchive>().DeleteAsync(_user, default);
        Assert.True(cleanup.Complete, string.Join(",", cleanup.Issues));
        Assert.Empty(Directory.GetFiles(_email.SinkRoot!));
        Assert.Empty(await provider.GetRequiredService<IArtifactPublicationStore>().GetOwnerPageAsync(_user, null, 100, default));
        await provider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
    }
}
