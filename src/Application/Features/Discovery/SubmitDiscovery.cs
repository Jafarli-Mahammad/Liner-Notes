using System.Security.Cryptography;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Domain.Discovery;
using MediatR;

namespace LinerNotes.Application.Features.Discovery;

public sealed record SubmitDiscovery(Guid ProfileId, long Revision, string RequestKey, DiscoveryTasteSelection? Selection)
    : IRequest<DiscoveryJob>;

public sealed class SubmitDiscoveryHandler(IDiscoveryBatchStore batches, IDiscoveryJobStore jobs,
    IDiscoveryTasteStore taste, SelectDiscoveryTaste selections, IDiscoveryWriteCoordinator admission,
    DiscoveryRuntimeOptions options, TimeProvider clock) : IRequestHandler<SubmitDiscovery, DiscoveryJob>
{
    public async Task<DiscoveryJob> Handle(SubmitDiscovery request, CancellationToken ct)
    {
        options.Validate();
        if (!options.RunJobs) throw new GenerationStoppedException("discovery_worker_not_ready");
        if (request.Selection is { } selection && (selection.CatalogRecordIds is null || selection.Tags is null)) throw new ArgumentException("invalid_discovery_selection");
        object intent = request.Selection?.LegacySignalIds is { Count: > 0 } legacy
            ? new
            {
                Version = "discovery-intent-v2",
                request.Revision,
                Records = request.Selection.CatalogRecordIds.Order().ToArray(),
                Tags = request.Selection.Tags.Select(t => (t ?? "").Trim().ToLowerInvariant()).Order(StringComparer.Ordinal).ToArray(),
                Legacy = legacy.Order().ToArray()
            }
            : new
            {
                Version = "discovery-intent-v1",
                request.Revision,
                Records = request.Selection?.CatalogRecordIds.Order().ToArray(),
                Tags = request.Selection?.Tags.Select(t => (t ?? "").Trim().ToLowerInvariant()).Order(StringComparer.Ordinal).ToArray()
            };
        string hash = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(intent)));
        // Validate before persistence, including key syntax and nonnegative revision.
        _ = new DiscoveryJob(request.ProfileId, request.RequestKey, hash, DiscoveryJobKind.Generation, request.Revision, "{}", clock.GetUtcNow());
        var inputs = await batches.ReadInputsAsync(request.ProfileId, clock.GetUtcNow(), options.FeedbackWindow, ct)
            ?? throw new UnauthorizedAccessException();
        var prior = await jobs.FindByKeyAsync(request.ProfileId, request.RequestKey, ct);
        if (prior is not null) return Replay(prior);
        var signals = request.Selection is null ? null : await selections.ResolveAsync(request.ProfileId, request.Selection, ct, inputs.Signals);
        if ((signals ?? inputs.Signals).Count == 0 && OnboardingState.Read(inputs.Profile.OnboardingJson)?.AllStyles != true) throw new ArgumentException("discovery_taste_required");
        return await admission.ExecuteAsync(1_048_576, async token =>
        {
            var existing = await jobs.FindByKeyAsync(request.ProfileId, request.RequestKey, token);
            if (existing is not null) return Replay(existing);
            long revision = signals is null ? request.Revision : await taste.ReplaceManualAsync(request.ProfileId,
                request.Revision, signals, clock.GetUtcNow(), token);
            string payload = JsonSerializer.Serialize(new { Kind = request.Selection is null ? DiscoveryBatchKind.OnDemand : DiscoveryBatchKind.Initial });
            return await jobs.EnqueueAsync(request.ProfileId, request.RequestKey, hash, DiscoveryJobKind.Generation, revision,
                payload, clock.GetUtcNow(), options.QueueCapacity, TimeSpan.FromSeconds(options.CooldownSeconds), token);
        }, ct);

        DiscoveryJob Replay(DiscoveryJob job)
        {
            if (job.RequestHash != hash || job.Kind != DiscoveryJobKind.Generation)
                throw new DiscoveryRequestConflictException("idempotency_key_reused");
            return job;
        }
    }
}
