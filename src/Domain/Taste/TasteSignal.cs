using LinerNotes.Domain.Common;
using LinerNotes.Domain.Enums;

namespace LinerNotes.Domain.Taste;

/// <summary>
/// An atomic, provenance-tagged record representing a discrete taste signal for a user.
/// Stored verbatim to satisfy transparency, "your data" exports, and privacy-first deletion.
/// </summary>
public sealed class TasteSignal : AuditableEntity
{
    public Guid? CatalogRecordId { get; private set; }
    public Guid? CanonicalEntityId { get; private set; }
    public string? SelectionJson { get; private set; }

    public void SetCatalogSelection(Guid recordId, Guid canonicalId, string evidenceJson)
    {
        if (Source != TasteSignalSource.InitialSeedManual || TargetType is not (TasteTargetType.Artist or TasteTargetType.Album or TasteTargetType.Track) ||
            recordId == Guid.Empty || canonicalId == Guid.Empty || string.IsNullOrWhiteSpace(evidenceJson) ||
            System.Text.Encoding.UTF8.GetByteCount(evidenceJson) > 24_576)
            throw new ArgumentException("invalid_catalog_selection");
        CatalogRecordId = recordId;
        CanonicalEntityId = canonicalId;
        SelectionJson = evidenceJson;
    }

    public Guid? UserId { get; private set; }
    public Guid ProfileId { get; private set; }
    public TasteTargetType TargetType { get; private set; }
    public string TargetValue { get; private set; }
    public string NormalizedTargetValue { get; private set; }
    public double Weight { get; private set; }
    public TasteSignalSource Source { get; private set; }
    public string Context { get; private set; }

    // Parameterless constructor for EF Core
    private TasteSignal()
    {
        TargetValue = string.Empty;
        NormalizedTargetValue = string.Empty;
        Context = string.Empty;
    }

    public TasteSignal(
        Guid userId,
        TasteTargetType targetType,
        string targetValue,
        double weight,
        TasteSignalSource source,
        string context,
        Guid? id = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        if (string.IsNullOrWhiteSpace(targetValue))
            throw new ArgumentException("TargetValue cannot be empty.", nameof(targetValue));
        if (string.IsNullOrWhiteSpace(context))
            throw new ArgumentException("Context description is required for transparency provenance.", nameof(context));

        if (id.HasValue) Id = id.Value;
        UserId = userId;
        ProfileId = userId;
        TargetType = targetType;
        TargetValue = targetValue.Trim();
        NormalizedTargetValue = TargetValue.ToLowerInvariant();
        Weight = weight;
        Source = source;
        Context = context.Trim();
        CreatedAt = DateTime.UtcNow;
    }

    public static TasteSignal ForProfile(Guid profileId, Guid? userId, TasteTargetType type, string value, double weight, TasteSignalSource source, string context)
    {
        if (profileId == Guid.Empty || userId == Guid.Empty) throw new ArgumentException("Invalid owner.");
        return new TasteSignal(profileId, type, value, weight, source, context) { UserId = userId };
    }
    public void AttachRegisteredOwner(Guid userId, Guid profileId)
    {
        if (userId == Guid.Empty || ProfileId != profileId || (UserId.HasValue && UserId != userId))
            throw new ArgumentException("Invalid profile owner.");
        UserId = userId;
    }
    public void BindRegisteredProfile(Guid userId, Guid profileId)
    {
        if (UserId != userId || profileId == Guid.Empty) throw new ArgumentException("Invalid profile binding.");
        ProfileId = profileId;
    }
    public static TasteSignal CreateSeedTag(Guid userId, string tag, double weight, string context) =>
            new(userId, TasteTargetType.Tag, tag, weight, TasteSignalSource.InitialSeedManual, context);

    public static TasteSignal CreateSeedArtist(Guid userId, string artist, double weight, string context) =>
        new(userId, TasteTargetType.Artist, artist, weight, TasteSignalSource.InitialSeedManual, context);

    public static TasteSignal CreateFromLike(Guid userId, string item, TasteTargetType type, string context) =>
        new(userId, type, item, 1.0, TasteSignalSource.RecommendationLike, context);

    public static TasteSignal CreateFromDislike(Guid userId, string item, TasteTargetType type, string context) =>
        new(userId, type, item, -1.0, TasteSignalSource.RecommendationDislike, context);

    public override string ToString() =>
        $"[{Source}] {TargetType}:{TargetValue} (weight: {Weight:+0.00;-0.00}) — {Context}";
}
