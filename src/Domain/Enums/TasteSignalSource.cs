namespace LinerNotes.Domain.Enums;

/// <summary>
/// Provenance of a taste signal explaining where and why it originated.
/// Essential for transparency and data export compliance.
/// </summary>
public enum TasteSignalSource
{
    InitialSeedManual = 1,
    InitialSeedLastFm = 2,
    InitialSeedListenBrainz = 3,
    RecommendationLike = 4,
    RecommendationDislike = 5,
    ExplicitFeedback = 6,
    ListeningHistoryImport = 7,
    RecommendationAlreadyKnown = 8,
    RecommendationRating = 9
}
