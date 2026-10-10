using System.Globalization;
using LinerNotes.Domain.Scoring;

namespace LinerNotes.Application.Common.Models.Recommendation;

public sealed class GenerationConfiguration
{
    public const string SectionName = "Generation";
    public string ConfigurationVersion { get; set; } = "phase6-config-v1";
    public double TagWeight { get; set; } = 1;
    public double NoveltyWeight { get; set; } = 0.2;
    public double FeedbackTagWeight { get; set; } = 0.5;
    public double MinimumPositiveFeedbackWeight { get; set; } = 0.4;
    public int MaximumArtistSeeds { get; set; } = 20;
    public int MaximumTagSeeds { get; set; } = 20;
    public int DiscoveryLimit { get; set; } = 10;
    public int MaximumCandidates { get; set; } = 400;
    public int MaximumSnapshotBytes { get; set; } = 100_000;
    public int MaximumPicks { get; set; } = 5;
    public EvidenceOrigin Origin { get; set; } = EvidenceOrigin.Recorded;
    public DateTimeOffset? RecordingExpiresAtUtc { get; set; }
    public string? ApprovedRecordingManifestSha256 { get; set; }

    public BaselineAConfiguration Scoring()
    {
        if (MaximumArtistSeeds is < 1 or > 20 || MaximumTagSeeds is < 1 or > 20 || DiscoveryLimit is < 1 or > 10 ||
            MaximumCandidates is < 1 or > 400 || MaximumSnapshotBytes is < 1 or > 100_000 || MaximumPicks is < 1 or > 5 ||
            Origin is not (EvidenceOrigin.Recorded or EvidenceOrigin.Synthetic) ||
            !double.IsFinite(FeedbackTagWeight) || FeedbackTagWeight < 0 ||
            !double.IsFinite(MinimumPositiveFeedbackWeight) || MinimumPositiveFeedbackWeight is < 0 or > 1)
            throw new ArgumentException("Invalid offline generation configuration or mechanical limits.");
        var policy = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(MaximumArtistSeeds)] = MaximumArtistSeeds.ToString(CultureInfo.InvariantCulture),
            [nameof(MaximumTagSeeds)] = MaximumTagSeeds.ToString(CultureInfo.InvariantCulture),
            [nameof(DiscoveryLimit)] = DiscoveryLimit.ToString(CultureInfo.InvariantCulture),
            [nameof(MaximumCandidates)] = MaximumCandidates.ToString(CultureInfo.InvariantCulture),
            [nameof(MaximumSnapshotBytes)] = MaximumSnapshotBytes.ToString(CultureInfo.InvariantCulture),
            [nameof(MaximumPicks)] = MaximumPicks.ToString(CultureInfo.InvariantCulture),
            [nameof(FeedbackTagWeight)] = FeedbackTagWeight.ToString("R", CultureInfo.InvariantCulture),
            [nameof(MinimumPositiveFeedbackWeight)] = MinimumPositiveFeedbackWeight.ToString("R", CultureInfo.InvariantCulture),
            [nameof(Origin)] = Origin.ToString(),
            [nameof(RecordingExpiresAtUtc)] = RecordingExpiresAtUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? "unconfigured",
            [nameof(ApprovedRecordingManifestSha256)] = ApprovedRecordingManifestSha256 ?? "unconfigured"
        };
        var scoring = new BaselineAConfiguration(ConfigurationVersion, TagWeight, NoveltyWeight, policy);
        scoring.Validate();
        return scoring;
    }
}
