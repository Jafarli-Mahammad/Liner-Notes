using LinerNotes.Domain.Common;

namespace LinerNotes.Domain.Catalog;

/// <summary>
/// Musical album/release record.
/// </summary>
public sealed class Album : BaseEntity
{
    public string Title { get; private set; }
    public string ArtistName { get; private set; }
    public string? Mbid { get; private set; }
    public Guid? ArtistId { get; private set; }

    // Parameterless constructor for EF Core
    private Album()
    {
        Title = string.Empty;
        ArtistName = string.Empty;
    }

    public Album(string title, string artistName, string? mbid = null, Guid? artistId = null, Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Album title cannot be empty.", nameof(title));
        if (string.IsNullOrWhiteSpace(artistName))
            throw new ArgumentException("Artist name cannot be empty.", nameof(artistName));

        if (id.HasValue) Id = id.Value;
        Title = title.Trim();
        ArtistName = artistName.Trim();
        Mbid = string.IsNullOrWhiteSpace(mbid) ? null : mbid.Trim();
        ArtistId = artistId;
    }

    public static Album Create(string title, string artistName, string? mbid = null, Guid? artistId = null) =>
        new(title, artistName, mbid, artistId);

    public override string ToString() => $"{ArtistName} - {Title}";
}
