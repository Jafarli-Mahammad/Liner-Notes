using LinerNotes.Domain.Catalog;

namespace LinerNotes.Domain.Scoring;

/// <summary>
/// A candidate track presented to the scorer for evaluation and ranking.
/// </summary>
public sealed class CandidateTrack
{
    public Track Track { get; }
    public WeightedTagVector TagVector { get; }

    /// <summary>
    /// Global popularity score in [0.0, 1.0], where 0.0 is completely obscure/underground
    /// and 1.0 represents a global mainstream superstar (e.g. Drake, Taylor Swift).
    /// Used by the anti-popularity penalty mechanic.
    /// </summary>
    public double GlobalPopularity { get; }

    /// <summary>
    /// Explicit familiarity override in [0.0, 1.0] if known from upstream scrobbles.
    /// If null, familiarity is evaluated directly against the UserTasteProfile.
    /// </summary>
    public double? ExplicitFamiliarity { get; }

    public string CandidateKey => Track.TrackKey;

    public CandidateTrack(
        Track track,
        WeightedTagVector tagVector,
        double globalPopularity,
        double? explicitFamiliarity = null)
    {
        Track = track ?? throw new ArgumentNullException(nameof(track));
        TagVector = tagVector ?? throw new ArgumentNullException(nameof(tagVector));

        if (globalPopularity < 0.0 || globalPopularity > 1.0)
            throw new ArgumentOutOfRangeException(nameof(globalPopularity), "Global popularity must be between 0.0 and 1.0.");

        if (explicitFamiliarity.HasValue && (explicitFamiliarity.Value < 0.0 || explicitFamiliarity.Value > 1.0))
            throw new ArgumentOutOfRangeException(nameof(explicitFamiliarity), "Explicit familiarity must be between 0.0 and 1.0.");

        GlobalPopularity = globalPopularity;
        ExplicitFamiliarity = explicitFamiliarity;
    }

    public static CandidateTrack Create(
        Track track,
        WeightedTagVector tagVector,
        double globalPopularity = 0.0) =>
        new(track, tagVector, globalPopularity);

    public override string ToString() => $"{Track} (Pop: {GlobalPopularity:F2})";
}
