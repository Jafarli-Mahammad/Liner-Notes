using LinerNotes.Application.DTOs.Digests;
using LinerNotes.Application.DTOs.Export;
using LinerNotes.Application.DTOs.Subscribers;
using LinerNotes.Application.DTOs.Taste;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Taste;

namespace LinerNotes.Application.Common.Mappings;

/// <summary>
/// High-performance, zero-reflection mapping extension methods adhering to modern C# standards.
/// </summary>
public static class MappingExtensions
{
    public static SubscriberDto ToDto(this User user) => new(
        user.Id,
        user.Email,
        user.TimeZone,
        user.DeliveryDay,
        user.DeliveryHourUtc,
        user.NextDigestAt,
        user.CreatedAt);

    public static TasteSignalDto ToDto(this TasteSignal signal) => new(
        signal.Id,
        signal.UserId,
        signal.TargetType,
        signal.TargetValue,
        signal.NormalizedTargetValue,
        signal.Weight,
        signal.Source,
        signal.Context,
        signal.CreatedAt);

    public static TrackDto ToDto(this Track track) => new(
        track.Id,
        track.Title,
        track.ArtistName,
        track.AlbumTitle,
        track.Mbid,
        track.DurationSeconds,
        track.ExternalSpotifyUrl,
        track.ExternalYoutubeUrl);

    public static ScoreBreakdownDto ToDto(this ScoreBreakdown breakdown) => new(
        breakdown.FinalScore,
        breakdown.TagSimilarityScore,
        breakdown.PopularityPenalty,
        breakdown.NoveltyBoost,
        breakdown.FeedbackPenalty,
        breakdown.MatchedTags.Select(m => new MatchedTagContributionDto(
            m.TagName,
            m.CandidateTagWeight,
            m.UserTasteWeight,
            m.ContributionProduct)).ToList());

    public static WeeklyRecommendationDto ToDto(this WeeklyRecommendation recommendation) => new(
        recommendation.Id,
        recommendation.Rank,
        recommendation.Track.ToDto(),
        recommendation.ScoreBreakdown.ToDto(),
        recommendation.WhyThisPick(),
        recommendation.Feedback,
        recommendation.FeedbackComment,
        recommendation.FeedbackGivenAt);

    public static WeeklyDigestDto ToDto(this WeeklyDigest digest) => new(
        digest.Id,
        digest.UserId,
        digest.Week.Value,
        digest.WeekStartDate,
        digest.WeekEndDate,
        digest.Status,
        digest.SentAt,
        digest.Recommendations.Select(r => r.ToDto()).ToList());

    public static UserMusicConnectionExportDto ToExportDto(this UserMusicConnection connection) => new(
        connection.ServiceType.ToString(),
        connection.ExternalUsername,
        connection.LastSyncedAt,
        connection.IsActive,
        connection.CreatedAt);
}
