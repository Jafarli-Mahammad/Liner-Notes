using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Domain.Discovery;

namespace LinerNotes.Application.Features.Discovery;

public sealed class RunDiscoveryJob(IDiscoveryBatchStore batches,
    PrepareDiscoveryBatch preparation, IDiscoveryWriteCoordinator admission, DiscoveryRuntimeOptions options, TimeProvider clock,
    IArtworkQueue? artwork = null)
{
    public async Task ExecuteAsync(DiscoveryJob job, bool allowAcquisition, CancellationToken ct)
    {
        options.Validate();
        if (job.Kind != DiscoveryJobKind.Generation || job.LeaseToken is not { } token)
            throw new InvalidOperationException("generation_lease_required");
        var inputs = await batches.ReadInputsAsync(job.ProfileId, clock.GetUtcNow(), options.FeedbackWindow, ct)
            ?? throw new InvalidOperationException("job_inputs_changed");
        if (inputs.Profile.Revision != job.InputRevision) throw new InvalidOperationException("job_inputs_changed");
        using var payload = JsonDocument.Parse(job.PayloadJson);
        if (!payload.RootElement.TryGetProperty("Kind", out var value) || !value.TryGetInt32(out int rawKind) ||
            rawKind is not ((int)DiscoveryBatchKind.Initial) and not ((int)DiscoveryBatchKind.OnDemand))
            throw new GenerationStoppedException("job_payload_invalid");
        var prepared = await preparation.PrepareAsync(inputs, Guid.NewGuid(), allowAcquisition, ct);
        await admission.ExecuteAsync(prepared.ReservationBytes, async cancellation => {
            await batches.CompleteAsync(job.Id, token, prepared.Id, (DiscoveryBatchKind)rawKind, prepared.Picks,
                prepared.CoverageJson, prepared.EvidenceExpiry, clock.GetUtcNow(), cancellation);
            return true;
        }, ct);
        artwork?.Enqueue(prepared.Picks.Select(p => Guid.TryParse(p.Track.Mbid, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty).Distinct().ToArray(), priority: true);
    }
}
