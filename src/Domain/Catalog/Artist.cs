using LinerNotes.Domain.Common;

namespace LinerNotes.Domain.Catalog;

/// <summary>
/// Musical artist entity with MusicBrainz identifier and normalized name for external mapping.
/// </summary>
public sealed class Artist : BaseEntity
{
    public string Name { get; private set; }
    public string NormalizedName { get; private set; }
    public string? Mbid { get; private set; }

    // Parameterless constructor for EF Core
    private Artist()
    {
        Name = string.Empty;
        NormalizedName = string.Empty;
    }

    public Artist(string name, string? mbid = null, Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Artist name cannot be empty.", nameof(name));

        if (id.HasValue) Id = id.Value;
        Name = name.Trim();
        NormalizedName = Name.ToLowerInvariant();
        Mbid = string.IsNullOrWhiteSpace(mbid) ? null : mbid.Trim();
    }

    public static Artist Create(string name, string? mbid = null) => new(name, mbid);

    public void UpdateMbid(string mbid)
    {
        if (string.IsNullOrWhiteSpace(mbid))
            throw new ArgumentException("MBID cannot be empty.", nameof(mbid));
        Mbid = mbid.Trim();
    }

    public override string ToString() => Name;
}
