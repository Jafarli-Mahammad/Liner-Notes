using LinerNotes.Domain.Catalog;

namespace LinerNotes.Application.Common.Interfaces.Catalog;

public sealed record CatalogItemDto(Guid Id, Guid CanonicalId, CatalogKind Kind, string Name, string? ArtistName,
    string? Disambiguation, string? FirstReleaseDate, IReadOnlyList<string> Tags, string Source,
    string Attribution, Guid Version, CatalogOrigin Origin,
    IReadOnlyList<CatalogArtistCreditDto>? Artists = null, string MetadataLicense = "", string? TagLicense = null,
    IReadOnlyList<CatalogTagInput>? TagEvidence = null, DateTimeOffset? AcquiredAtUtc = null,
    DateTimeOffset? ExpiresAtUtc = null, string? ContentSha256 = null, string NormalizationVersion = CatalogText.Version);
public sealed record CatalogSearchResult(IReadOnlyList<CatalogItemDto> Items, bool HasMore, string NormalizationVersion);
public sealed record CatalogTagInput(string Name, double Weight, string? Scope = null);
public sealed record CatalogArtifactRow(Guid Id, CatalogKind Kind, string Name, string[] Aliases, Guid[] ArtistIds,
    CatalogTagInput[] Tags, string? Disambiguation = null, string? FirstReleaseDate = null);

public interface ICatalogReader
{
    Task<CatalogSearchResult> SearchAsync(string query, CatalogKind kind, int limit, int offset, DateTimeOffset now, CancellationToken ct);
    Task<CatalogItemDto?> GetAsync(Guid recordId, DateTimeOffset now, CancellationToken ct);
}

public interface ICatalogImportStore
{
    Task<CatalogPublication?> FindAsync(Guid publicationId, CancellationToken ct);
    Task StartAsync(CatalogPublication publication, CancellationToken ct);
    Task AppendAsync(Guid publicationId, int expectedCursor, IReadOnlyList<CatalogArtifactRow> rows, CancellationToken ct);
    Task VerifyRowsAsync(Guid publicationId, IReadOnlyList<CatalogArtifactRow> rows, CancellationToken ct);
    Task PublishAsync(Guid publicationId, DateTimeOffset now, CancellationToken ct);
}

/// <summary>Only an explicit durable lookup worker may call this; autocomplete has no dependency on it.</summary>
public interface ICatalogLookupSource
{
    Task<CatalogLookupResult> SearchAsync(string query, CatalogKind kind, CancellationToken ct);
}
public interface ICatalogLookupPreparation
{
    Task<CatalogLookupResult> SearchAsync(string query, CatalogKind kind, CancellationToken ct);
}

public interface ICatalogArtifactReader
{
    IAsyncEnumerable<CatalogArtifactRow> ReadAsync(Stream stream, CatalogPublication publication, CancellationToken ct);
}

public sealed record CatalogLookupResult(IReadOnlyList<CatalogArtifactRow> Records, IReadOnlyList<Guid> MatchedCanonicalIds,
    CatalogOrigin Origin, string Source, string SourceUri, DateTimeOffset AcquiredAtUtc, DateTimeOffset? CacheUntilUtc, string ResponseSha256);

public sealed record CatalogArtistCreditDto(Guid CanonicalId, string Name, int Position);

public static class CatalogRowFingerprint
{
    public static string Sha256(CatalogArtifactRow row) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
        System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(row)));
}
