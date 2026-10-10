using LinerNotes.Infrastructure.RecommendationSources.LastFm;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Application.Features.Digests.Commands.GenerateDigest;
using LinerNotes.Application.Features.Digests.Commands.CaptureLocalDigest;
using LinerNotes.Infrastructure.Email;
using LinerNotes.Infrastructure.Storage;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace LinerNotes.Worker;

public sealed record LocalWorkerResult(string Status, string? Reason=null, Guid? DigestId=null, long? AccountedBytes=null);

public sealed class LocalDigestWorkerRunner(IServiceProvider services)
{
    public async Task<LocalWorkerResult> RunAsync(LocalWorkerOptions options,CancellationToken ct=default)
    {
        await using var scope=services.CreateAsyncScope();var sp=scope.ServiceProvider;
        var generation=sp.GetRequiredService<GenerationConfiguration>();
        if(generation.Origin!=options.Origin) return new("stopped","origin_mismatch");
        generation.Scoring();
        if(options.Action=="reconcile")
        {
            // Provision shared keys before measuring the whole database. Token is never logged or retained here.
            if(sp.GetRequiredService<LocalEmailOptions>().Enabled) _=sp.GetRequiredService<IUnsubscribeTokens>().Create(options.UserId);
            var state=await sp.GetRequiredService<IDigestGenerationStore>().ReadStorageAsync(ct);
            var inventory=await sp.GetRequiredService<LocalGenerationStorage>().ReconcileAsync(state,ct);
            return new("reconciled",AccountedBytes:inventory.DatabaseBytes+inventory.Categories.Values.Sum(c=>c.Bytes));
        }
        if(options.Action=="generate")
        {
            if(options.Origin==EvidenceOrigin.Recorded &&
                await sp.GetRequiredService<IDigestGenerationStore>().GetExistingAsync(options.UserId,options.Week,ct) is null)
            {
                var recording=await sp.GetRequiredService<LocalRecordedInputLoader>().LoadAsync(options.UserId,options.Week,ct);
                generation.RecordingExpiresAtUtc=recording.ExpiresAtUtc;
                generation.ApprovedRecordingManifestSha256=recording.ManifestSha256;
                sp.GetRequiredService<LocalRecordedInputs>().Set(recording);
            }
            var result=await sp.GetRequiredService<ISender>().Send(new GenerateDigestCommand(options.UserId,options.Week),ct);
            return new(result.Status,result.Reason,result.Digest?.Id);
        }
        if(options.Action=="capture")
        {
            var result=await sp.GetRequiredService<ISender>().Send(new CaptureLocalDigestCommand(options.UserId,options.Week),ct);
            return new(result.Status,result.Reason,result.DigestId);
        }
        return new("stopped","explicit_action_required");
    }
}
