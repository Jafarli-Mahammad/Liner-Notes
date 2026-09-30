using LinerNotes.Domain.Catalog;

namespace LinerNotes.Domain.Taste;

/// <summary>
/// Immutable snapshot representing a user's musical preferences fed into the pure scorer.
/// Combines weighted tag vectors with familiarity and explicit rejection history.
/// </summary>
public sealed class UserTasteProfile
{
    public Guid UserId { get; }
    public WeightedTagVector TagPreferences { get; }
    public IReadOnlySet<string> FamiliarArtistNames { get; }
    public IReadOnlySet<string> FamiliarTrackKeys { get; }
    public IReadOnlySet<string> RejectedArtistNames { get; }
    public IReadOnlySet<string> RejectedTrackKeys { get; }

    public UserTasteProfile(
        Guid userId,
        WeightedTagVector tagPreferences,
        IReadOnlySet<string>? familiarArtistNames = null,
        IReadOnlySet<string>? familiarTrackKeys = null,
        IReadOnlySet<string>? rejectedArtistNames = null,
        IReadOnlySet<string>? rejectedTrackKeys = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));

        UserId = userId;
        TagPreferences = tagPreferences ?? throw new ArgumentNullException(nameof(tagPreferences));

        // Normalize sets to lowercase for case-insensitive lookup
        FamiliarArtistNames = NormalizeSet(familiarArtistNames);
        FamiliarTrackKeys = NormalizeSet(familiarTrackKeys);
        RejectedArtistNames = NormalizeSet(rejectedArtistNames);
        RejectedTrackKeys = NormalizeSet(rejectedTrackKeys);
    }

    public static UserTasteProfile Create(Guid userId, WeightedTagVector tagPreferences) =>
        new(userId, tagPreferences);

    public bool IsArtistFamiliar(string artistName) =>
        !string.IsNullOrWhiteSpace(artistName) &&
        FamiliarArtistNames.Contains(artistName.Trim().ToLowerInvariant());

    public bool IsTrackFamiliar(string trackKey) =>
        !string.IsNullOrWhiteSpace(trackKey) &&
        FamiliarTrackKeys.Contains(trackKey.Trim().ToLowerInvariant());

    public bool IsArtistRejected(string artistName) =>
        !string.IsNullOrWhiteSpace(artistName) &&
        RejectedArtistNames.Contains(artistName.Trim().ToLowerInvariant());

    public bool IsTrackRejected(string trackKey) =>
        !string.IsNullOrWhiteSpace(trackKey) &&
        RejectedTrackKeys.Contains(trackKey.Trim().ToLowerInvariant());

    private static HashSet<string> NormalizeSet(IEnumerable<string>? source)
    {
        if (source is null) return new HashSet<string>(StringComparer.Ordinal);

        var normalized = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in source)
        {
            if (!string.IsNullOrWhiteSpace(item))
            {
                normalized.Add(item.Trim().ToLowerInvariant());
            }
        }
        return normalized;
    }
}
