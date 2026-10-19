using LinerNotes.Domain.Common;

namespace LinerNotes.Domain.Catalog;

/// <summary>Public acquisition provenance and import progress, never private search history.</summary>
public sealed class CatalogPublication : BaseEntity
{
    public string Source { get; private set; } = "";
    public string SourceVersion { get; private set; } = "";
    public string SourceUri { get; private set; } = "";
    public CatalogOrigin Origin { get; private set; }
    public string MetadataLicense { get; private set; } = "";
    public string? TagLicense { get; private set; }
    public string Attribution { get; private set; } = "";
    public string ContentSha256 { get; private set; } = "";
    public long ArtifactBytes { get; private set; }
    public int ExpectedRecords { get; private set; }
    public int ImportedRecords { get; private set; }
    public CatalogPublicationState State { get; private set; }
    public DateTimeOffset AcquiredAtUtc { get; private set; }
    public DateTimeOffset? ExpiresAtUtc { get; private set; }
    public DateTimeOffset? PublishedAtUtc { get; private set; }
    public string NormalizationVersion { get; private set; } = CatalogText.Version;
    public bool IsLookup { get; private set; }
    private CatalogPublication() { }

    public CatalogPublication(Guid id, string source, string sourceVersion, string sourceUri, CatalogOrigin origin,
        string metadataLicense, string? tagLicense, string attribution, string contentSha256, long artifactBytes,
        int expectedRecords, DateTimeOffset acquiredAtUtc, DateTimeOffset? expiresAtUtc = null, bool isLookup = false)
    {
        if (id == Guid.Empty || !Enum.IsDefined(origin) || artifactBytes is < 1 or > 268_435_456 || expectedRecords is < 1 or > 500_000 ||
            acquiredAtUtc.Offset != TimeSpan.Zero || (expiresAtUtc.HasValue && (expiresAtUtc.Value.Offset != TimeSpan.Zero || expiresAtUtc <= acquiredAtUtc)))
            throw new ArgumentException("catalog_manifest_invalid");
        static string Text(string value, int limit) => !string.IsNullOrWhiteSpace(value) && value.Length <= limit && !value.Any(char.IsControl)
            ? value.Trim() : throw new ArgumentException("catalog_manifest_text_invalid");
        if (contentSha256.Length != 64 || contentSha256.Any(c => c is not (>= 'a' and <= 'f') and not (>= '0' and <= '9')))
            throw new ArgumentException("catalog_artifact_hash_invalid");
        if (origin == CatalogOrigin.Public && (!Uri.TryCreate(sourceUri, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query)))
            throw new ArgumentException("catalog_source_uri_invalid");
        Id = id; Source = Text(source, 128); SourceVersion = Text(sourceVersion, 128); SourceUri = Text(sourceUri, 1000);
        Origin = origin; MetadataLicense = Text(metadataLicense, 128); TagLicense = tagLicense is null ? null : Text(tagLicense, 128);
        Attribution = Text(attribution, 1000); ContentSha256 = contentSha256; ArtifactBytes = artifactBytes;
        ExpectedRecords = expectedRecords; AcquiredAtUtc = acquiredAtUtc; ExpiresAtUtc = expiresAtUtc; IsLookup = isLookup;
    }

    public void Advance(int expectedCursor, int count)
    {
        if (State != CatalogPublicationState.Staging || ImportedRecords != expectedCursor || count < 1 || ImportedRecords + count > ExpectedRecords)
            throw new InvalidOperationException("catalog_import_cursor_conflict");
        ImportedRecords += count;
    }
    public void Publish(DateTimeOffset now)
    {
        if (State != CatalogPublicationState.Staging || ImportedRecords != ExpectedRecords || now.Offset != TimeSpan.Zero || now < AcquiredAtUtc || ExpiresAtUtc <= now)
            throw new InvalidOperationException("catalog_publication_incomplete_or_expired");
        State = CatalogPublicationState.Published; PublishedAtUtc = now;
    }
}

public sealed class CatalogHead
{
    public int Id { get; private set; } = 1;
    public Guid? PublicationId { get; private set; }
    public void Publish(Guid publicationId) => PublicationId = publicationId != Guid.Empty ? publicationId : throw new ArgumentException("catalog_publication_invalid");
}
