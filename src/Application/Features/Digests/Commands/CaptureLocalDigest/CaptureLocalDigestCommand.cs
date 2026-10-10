using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using MediatR;

namespace LinerNotes.Application.Features.Digests.Commands.CaptureLocalDigest;

public sealed record CaptureLocalDigestCommand(Guid UserId, IsoWeek Week) : IRequest<LocalCaptureResult>;
public sealed record LocalCaptureResult(string Status, Guid? DigestId = null, string? Reason = null);

/// <summary>Account ownership, artifact lease, then a non-replayed SQL row transaction.</summary>
public sealed class CaptureLocalDigestCommandHandler(ILocalDigestCaptureStore store, ILocalEmailSink sink, IAccountEmailLease ownership)
    : IRequestHandler<CaptureLocalDigestCommand, LocalCaptureResult>
{
    public async Task<LocalCaptureResult> Handle(CaptureLocalDigestCommand request, CancellationToken ct)
    {
        if (request.UserId == Guid.Empty || request.Week is null) return new("stopped", Reason:"invalid_user_or_week");
        await using var accountLease = await ownership.AcquireAsync(request.UserId, ct);
        for (int attempt=0; attempt<3; attempt++)
        {
            try
            {
                var inputs = await store.ReadAsync(request.UserId, request.Week, ct);
                Eligibility(inputs);
                await using var capture = await sink.OpenAsync(inputs.Digest!, inputs.Subscriber!, inputs.Storage, ct);
                await using var transaction = await store.BeginAsync(request.UserId, request.Week, ct);
                var fresh = transaction.Inputs;
                Eligibility(fresh);
                if (fresh.Digest!.Id != inputs.Digest!.Id) throw new GenerationStoppedException("digest_changed");
                await capture.PublishAsync(fresh.Digest, fresh.Subscriber!, fresh.Storage, ct);
                await transaction.CommitCapturedAsync(ct);
                return new(capture.ReceiptExists ? "already_captured" : "local_captured", fresh.Digest.Id);
            }
            catch (LocalCaptureTransientException) when (attempt<2) { await Task.Delay(attempt==0 ? 250 : 1000, ct); }
            catch (LocalCaptureTransientException) { return new("stopped", Reason:"capture_database_unavailable"); }
            catch (GenerationStoppedException ex)
            {
                // Bounded reason only. Never downgrade a completed receipt/state or imply a published file was rolled back.
                if (ex.Message is "email_io_unavailable" or "email_io_forbidden" or "email_alternative_too_large" or "email_message_too_large")
                    await store.RecordFailureAsync(request.UserId, request.Week, ex.Message, ct);
                return new("stopped", Reason:ex.Message);
            }
            catch (IOException) { return new("stopped", Reason:"email_io_unavailable"); }
            catch (UnauthorizedAccessException) { return new("stopped", Reason:"email_io_forbidden"); }
            catch (InvalidOperationException) { return new("stopped", Reason:"local_email_configuration_invalid"); }
        }
        return new("stopped", Reason:"capture_database_unavailable");
    }

    private static void Eligibility(LocalCaptureInputs inputs)
    {
        if (inputs.Subscriber is null || inputs.Subscriber.IsDeleted) throw new GenerationStoppedException("user_not_found");
        if (inputs.Subscriber.EmailUnsubscribedAtUtc is not null) throw new GenerationStoppedException("email_unsubscribed");
        if (inputs.Digest is null || inputs.Digest.IsDeleted) throw new GenerationStoppedException("digest_not_found");
        if (inputs.Digest.Status == DigestStatus.Sent) throw new GenerationStoppedException("digest_already_sent");
    }
}
