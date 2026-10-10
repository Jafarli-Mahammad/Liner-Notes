using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.DTOs.Digests;
using LinerNotes.Application.DTOs.Subscribers;

namespace LinerNotes.Application.Common.Interfaces;

public sealed record RenderedDigestEmail(Guid DigestId, Guid UserId, string Week, string Recipient,
    string TemplateVersion, string Fingerprint, string Token, string PlainText, string Html);

public interface IDigestEmailRenderer
{
    RenderedDigestEmail Render(WeeklyDigestDto digest, SubscriberDto subscriber, string? token = null);
}

public interface ILocalEmailStorage
{
    Task<ILocalEmailWriteLease> AcquireEmailAsync(GenerationStorageState database, long bytes,
        IReadOnlyList<string> ownedPaths, bool recovery, CancellationToken ct);
}

public interface ILocalEmailWriteLease : IAsyncDisposable
{
    Task RecordOwnWriteAsync(string path, CancellationToken ct);
    Task ValidateAsync(GenerationStorageState database, CancellationToken ct);
}

public interface ILocalEmailSink
{
    Task<ILocalEmailCapture> OpenAsync(WeeklyDigestDto digest, SubscriberDto subscriber,
        GenerationStorageState database, CancellationToken ct);
}

public interface ILocalEmailCapture : IAsyncDisposable
{
    bool ReceiptExists { get; }
    Task PublishAsync(WeeklyDigestDto digest, SubscriberDto subscriber, GenerationStorageState database, CancellationToken ct);
}
