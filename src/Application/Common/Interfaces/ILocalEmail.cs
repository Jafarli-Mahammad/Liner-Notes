using LinerNotes.Application.DTOs.Digests;
using LinerNotes.Application.DTOs.Subscribers;

namespace LinerNotes.Application.Common.Interfaces;

public interface IUnsubscribeTokenProtection
{
    string Protect(string value);
    string Unprotect(string value);
}

public interface IUnsubscribeTokens
{
    string Create(Guid userId);
    bool TryRead(string? token, out Guid userId);
}

public interface IUnsubscribeStore
{
    Task UnsubscribeAsync(Guid userId, DateTime utcNow, CancellationToken ct);
}

public interface IAccountEmailLease
{
    Task<IAsyncDisposable> AcquireAsync(Guid userId, CancellationToken ct);
}

public record LocalEmailCopyDto(Guid DigestId, string Week, string TemplateVersion, string Fingerprint,
    string PlainText, string Html, long Bytes);
public record LocalEmailArchiveDto(string Status, IReadOnlyList<LocalEmailCopyDto> Copies, IReadOnlyList<string> Issues);
public record LocalEmailCleanupResult(bool Complete, IReadOnlyList<string> Issues);
public interface ILocalEmailArchive
{
    Task<LocalEmailArchiveDto> ReadAsync(Guid userId, IReadOnlyList<WeeklyDigestDto> digests, CancellationToken ct);
    Task<LocalEmailCleanupResult> DeleteAsync(Guid userId, CancellationToken ct);
}
