using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.DTOs.Digests;
using LinerNotes.Application.DTOs.Subscribers;
using LinerNotes.Domain.Enums;

namespace LinerNotes.Infrastructure.Email;

public sealed partial class LocalEmailSink
{
    private async Task<ILocalEmailCapture> OpenManagedAsync(WeeklyDigestDto digest, SubscriberDto subscriber, CancellationToken ct)
    {
        if (publisher is null) throw new GenerationStoppedException("capture_publication_missing");
        string root = LocalEmailPaths.Root(options, storageOptions);
        string name = digest.Id.ToString("N") + ".eml.enc", final = Path.Combine(root, name);
        if (File.Exists(Path.Combine(root, digest.Id.ToString("N") + ".eml")))
            throw new GenerationStoppedException("email_plaintext_copy_requires_conversion");
        var operation = await publisher.FindAsync(root, name, ct);
        if (operation?.OwnerId is { } owner && owner != subscriber.Id)
            throw new GenerationStoppedException("email_copy_ownership_conflict");
        if (operation?.Phase == ArtifactPublicationPhase.Reserved)
        {
            await publisher.RecoverAsync(ct, operation.Id);
            operation = null;
        }
        if (operation?.Phase is ArtifactPublicationPhase.Prepared or ArtifactPublicationPhase.Publishing)
            operation = await publisher.PublishAsync(operation, ct);
        bool exists = File.Exists(final);
        RenderedDigestEmail message;
        if (exists)
        {
            var parsed = serializer.Parse(await LocalEmailPaths.ReadAsync(final, options.StoredMessageByteLimit, ct));
            message = renderer.Render(digest, subscriber, parsed.Receipt.Token);
            Validate(parsed, message);
        }
        else
        {
            if (digest.Status == DigestStatus.LocalCaptured || operation?.Phase == ArtifactPublicationPhase.Published)
                throw new GenerationStoppedException("email_receipt_missing");
            message = renderer.Render(digest, subscriber);
            operation = await publisher.StageAsync("copies", root, name, serializer.Serialize(message), ct, subscriber.Id);
        }
        return new ManagedCapture(operation, message, exists, publisher, renderer);
    }

    private sealed class ManagedCapture(ArtifactPublication? operation, RenderedDigestEmail message, bool exists,
        IRecoverableArtifactPublisher publisher, IDigestEmailRenderer renderer) : IRecoverableLocalEmailCapture
    {
        public bool ReceiptExists => exists;
        public async Task<ArtifactPublication?> PublishPreparedAsync(CancellationToken ct)
        {
            if (operation is null || operation.Phase == ArtifactPublicationPhase.Published) return null;
            operation = await publisher.PublishAsync(operation, ct);
            return operation;
        }
        public Task CompleteWithinAdmissionAsync(CancellationToken ct) => operation?.Phase == ArtifactPublicationPhase.Publishing
            ? publisher.CompleteWithinAdmissionAsync(operation, ct) : Task.CompletedTask;
        public Task PublishAsync(WeeklyDigestDto digest, SubscriberDto subscriber, GenerationStorageState database, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (subscriber.EmailUnsubscribedAtUtc is not null) throw new GenerationStoppedException("email_unsubscribed");
            if (renderer.Render(digest, subscriber, message.Token).Fingerprint != message.Fingerprint)
                throw new GenerationStoppedException("email_inputs_changed");
            return Task.CompletedTask;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
