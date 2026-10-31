using LinerNotes.Application;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.Application.Features.Export.Queries.GetUserDataExport;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Domain.Enums;
using LinerNotes.Infrastructure;
using LinerNotes.Infrastructure.Storage;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed partial class Phase8ScheduledWorkerPostgresTests
{
    private bool _managedCapture;
    private async Task EnableManagedCaptureAsync()
    {
        _managedCapture = true;
        await _services.DisposeAsync();
        var registrations = new ServiceCollection().AddLogging().AddApplication().AddDataAccess(_config).AddInfrastructure(_config);
        registrations.AddSingleton(_storage).AddSingleton(_email).AddSingleton<TimeProvider>(_clock);
        registrations.AddSingleton(new DiscoveryRuntimeOptions { EnforceStorageAdmission = true });
        registrations.AddSingleton(new CoordinatedStorageOptions { FilesystemPaths = [_root] });
        _services = registrations.BuildServiceProvider();
        await ReconcileAsync();
    }

    [LocalPostgresMigrationFact]
    public async Task ManagedScheduledCapturePublishesAccountsExportsAndAdvancesWithoutOperatorReconciliation()
    {
        await EnableManagedCaptureAsync();
        var result = Assert.Single((await TickAsync()).Jobs);
        Assert.True(result.Status == "advanced", $"Managed capture stopped: {result.Status}/{result.Reason}");
        await using var scope = _services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var digest = await provider.GetRequiredService<DataContext>().WeeklyDigests.SingleAsync();
        Assert.Equal(DigestStatus.LocalCaptured, digest.Status);
        var publication = await provider.GetRequiredService<IArtifactPublicationStore>().FindByNameAsync(_email.SinkRoot!, digest.Id.ToString("N") + ".eml.enc", default);
        Assert.NotNull(publication);
        Assert.Equal(_user, publication.OwnerId);
        Assert.Equal(ArtifactPublicationPhase.Published, publication.Phase);
        Assert.Equal(0, (await provider.GetRequiredService<IDigestGenerationStore>().ReadStorageAsync()).ReservedArtifactBytes);
        var export = await provider.GetRequiredService<ISender>().Send(new GetUserDataExportQuery(_user));
        Assert.Equal(publication.Id, Assert.Single(export.StorageArtifacts!).Id);
        var other = await provider.GetRequiredService<ISender>().Send(new GetUserDataExportQuery(_other));
        Assert.Empty(other.StorageArtifacts!);
        await provider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
        Assert.Empty((await TickAsync()).Jobs);
    }
}
