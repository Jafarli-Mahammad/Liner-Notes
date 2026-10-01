using LinerNotes.Domain.Common;
using LinerNotes.Domain.Enums;

namespace LinerNotes.Domain.Taste;

/// <summary>
/// An atomic, provenance-tagged record representing a discrete taste signal for a user.
/// Stored verbatim to satisfy transparency, "your data" exports, and privacy-first deletion.
/// </summary>
public sealed class TasteSignal : AuditableEntity
{
    public Guid UserId { get; private set; }
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
        TargetType = targetType;
        TargetValue = targetValue.Trim();
        NormalizedTargetValue = TargetValue.ToLowerInvariant();
        Weight = weight;
        Source = source;
        Context = context.Trim();
        CreatedAt = DateTime.UtcNow;
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
