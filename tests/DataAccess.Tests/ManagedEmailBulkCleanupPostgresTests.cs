using System.Diagnostics.Metrics;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Infrastructure.Storage;
using LinerNotes.Infrastructure.Email;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed partial class Phase8ScheduledWorkerPostgresTests
{
    [LocalPostgresMigrationFact]
    public async Task OwnedArchiveCleanupBatchesIntegrityWorkAndPreservesAnotherOwnersCopy()
    {
        await EnableManagedCaptureAsync();
        await using var scope = _services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var publisher = provider.GetRequiredService<IRecoverableArtifactPublisher>();
        var originals = new List<ArtifactPublication>();
        async Task<ArtifactPublication> Create(Guid owner, string name)
        {
            var email = new RenderedDigestEmail(Guid.NewGuid(), owner, LinerNotes.Domain.Digest.WeeklySchedulePolicy.Week(_due).Value,
                owner == _user ? "schedule@example.test" : "other@example.test", DigestEmailRenderer.Version, new string('a', 64),
                provider.GetRequiredService<IUnsubscribeTokens>().Create(owner), "private bulk fixture", "<p>private bulk fixture</p>");
            var bytes = provider.GetRequiredService<LocalEmailMessageSerializer>().Serialize(email);
            var operation = await publisher.StageAsync("copies", _email.SinkRoot!, name, bytes, default, owner);
            operation = await publisher.PublishAsync(operation, default);
            await provider.GetRequiredService<IDiscoveryWriteCoordinator>().PublishArtifactAsync(operation, 65_536, async ct =>
            {
                await publisher.CompleteWithinAdmissionAsync(operation, ct);
                return true;
            }, default);
            return operation;
        }
        for (int index = 0; index < 12; index++) originals.Add(await Create(_user, $"bulk-{index:D2}.eml.enc"));
        var other = await Create(_other, "other-owner.eml.enc");
        byte[] expectedOther = await File.ReadAllBytesAsync(Path.Combine(other.Root, other.FinalName));
        var active = new AsyncLocal<bool>();
        int admissions = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meter) =>
        {
            if (instrument.Meter.Name == "LinerNotes.Discovery" && instrument.Name == "discovery.stage.duration")
                meter.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, _, tags, _) =>
        {
            if (!active.Value) return;
            foreach (var tag in tags)
                if (tag.Key == "stage" && Equals(tag.Value, "storage.database_gate")) Interlocked.Increment(ref admissions);
        });
        listener.Start(); active.Value = true;
        var result = await provider.GetRequiredService<ILocalEmailArchive>().DeleteAsync(_user, default);
        active.Value = false;
        Assert.True(result.Complete, string.Join(",", result.Issues));
        Assert.InRange(admissions, 2, 4);
        Assert.Equal(expectedOther, await File.ReadAllBytesAsync(Path.Combine(other.Root, other.FinalName)));
        Assert.Single(Directory.GetFiles(_email.SinkRoot!));
        Assert.Empty(await provider.GetRequiredService<IArtifactPublicationStore>().GetOwnerPageAsync(_user, null, 100, default));
        await publisher.DeleteManyAsync(originals, default); // Replaying an acknowledged batch is harmless.
        await provider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
    }
}
