using LinerNotes.Domain.Common;

namespace LinerNotes.Domain.Catalog;

/// <summary>Immutable search projection/version metadata over shared Artist or Album identities.</summary>
public sealed class CatalogRecord : BaseEntity
{
    public Guid PublicationId { get; private set; }
    public Guid CanonicalId { get; private set; }
    public CatalogKind Kind { get; private set; }
    public Guid? ArtistId { get; private set; }
    public Guid? AlbumId { get; private set; }
    public string DisplayName { get; private set; } = "";
    public string? ArtistDisplayName { get; private set; }
    public string? Disambiguation { get; private set; }
    public string? FirstReleaseDate { get; private set; }
    public string AliasesJson { get; private set; } = "[]";
    public string TagsJson { get; private set; } = "[]";
    public string CreditsJson { get; private set; } = "[]";
    public string RowSha256 { get; private set; } = "";
    private CatalogRecord() { }
    public CatalogRecord(Guid publicationId, Guid canonicalId, CatalogKind kind, Guid entityId, string name,
        string? artistName, string? disambiguation, string? firstReleaseDate, string aliasesJson, string tagsJson, string rowSha256, string creditsJson = "[]")
    {
        if (publicationId == Guid.Empty || canonicalId == Guid.Empty || entityId == Guid.Empty || !Enum.IsDefined(kind) ||
            string.IsNullOrWhiteSpace(name) || name.Length > 256 || artistName?.Length > 1000 || disambiguation?.Length > 500 ||
            firstReleaseDate?.Length > 10 || System.Text.Encoding.UTF8.GetByteCount(aliasesJson) > 16_384 || System.Text.Encoding.UTF8.GetByteCount(tagsJson) > 16_384 || System.Text.Encoding.UTF8.GetByteCount(creditsJson) > 16_384)
            throw new ArgumentException("catalog_record_invalid");
        if (rowSha256.Length != 64 || rowSha256.Any(c => c is not (>= 'a' and <= 'f') and not (>= '0' and <= '9')))
            throw new ArgumentException("catalog_row_hash_invalid");
        RowSha256 = rowSha256;
        Id = CatalogText.RecordId(publicationId, kind, canonicalId); PublicationId = publicationId; CanonicalId = canonicalId; Kind = kind;
        ArtistId = kind == CatalogKind.Artist ? entityId : null; AlbumId = kind == CatalogKind.Album ? entityId : null;
        DisplayName = name.Trim(); ArtistDisplayName = artistName; Disambiguation = disambiguation; FirstReleaseDate = firstReleaseDate;
        AliasesJson = aliasesJson; TagsJson = tagsJson; CreditsJson = creditsJson;
    }
}

public sealed class CatalogSearchTerm
{
    public Guid RecordId { get; private set; }
    public CatalogKind Kind { get; private set; }
    public string NormalizedTerm { get; private set; } = "";
    private CatalogSearchTerm() { }
    public CatalogSearchTerm(Guid recordId, CatalogKind kind, string term)
    { RecordId = recordId; Kind = kind; NormalizedTerm = term; }
}

public sealed class AlbumCredit
{
    public Guid AlbumId { get; private set; }
    public Guid ArtistId { get; private set; }
    public int Position { get; private set; }
    private AlbumCredit() { }
    public AlbumCredit(Guid albumId, Guid artistId, int position)
    {
        if (albumId == Guid.Empty || artistId == Guid.Empty || position is < 0 or > 31) throw new ArgumentException("catalog_credit_invalid");
        AlbumId = albumId; ArtistId = artistId; Position = position;
    }
}
