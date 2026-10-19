using LinerNotes.Domain.Catalog;

namespace LinerNotes.Domain.Discovery;

/// <summary>Public music only. No recipient, private recommendation, source row, or event timestamp.</summary>
public sealed class RecentRecommendation
{
    public long Sequence { get; private set; }
    public Guid RecordingId { get; private set; }
    public string Title { get; private set; } = "";
    public string ArtistName { get; private set; } = "";
    public CatalogOrigin Origin { get; private set; }
    public string Attribution { get; private set; } = "";
    public DateTimeOffset? MetadataExpiresAtUtc { get; private set; }
    private RecentRecommendation() { }

    public RecentRecommendation(Guid recordingId, string title, string artistName, CatalogOrigin origin,
        string attribution, DateTimeOffset? metadataExpiresAtUtc)
    {
        if (recordingId == Guid.Empty || string.IsNullOrWhiteSpace(title) || title.Length > 256 ||
            string.IsNullOrWhiteSpace(artistName) || artistName.Length > 1000 || attribution.Length > 1000 || !Enum.IsDefined(origin))
            throw new ArgumentException("invalid_recent_music");
        RecordingId = recordingId; Title = title; ArtistName = artistName; Origin = origin;
        Attribution = attribution; MetadataExpiresAtUtc = metadataExpiresAtUtc;
    }
}
