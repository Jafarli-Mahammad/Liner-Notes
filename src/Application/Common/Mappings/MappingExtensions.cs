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
        user.CreatedAt, user.LastModifiedAt, user.CreatedBy, user.LastModifiedBy, user.DeletedBy, user.DeletedAt, user.IsDeleted);

    public static TasteSignalDto ToDto(this TasteSignal signal) => new(
        signal.Id,
        signal.UserId,
        signal.TargetType,
        signal.TargetValue,
        signal.NormalizedTargetValue,
        signal.Weight,
        signal.Source,
        signal.Context,
        signal.CreatedAt, signal.LastModifiedAt, signal.CreatedBy, signal.LastModifiedBy, signal.DeletedBy, signal.DeletedAt, signal.IsDeleted);

    public static TrackDto ToDto(this Track track) => new(
        track.Id,
        track.Title,
        track.ArtistName,
        track.AlbumTitle,
        track.Mbid,
        track.DurationSeconds,
        track.ExternalSpotifyUrl,
        track.ExternalYoutubeUrl, track.NormalizedTitle, track.NormalizedArtistName);

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
            m.ContributionProduct) { UnknownFields = m.UnknownFields }).ToList(),
        breakdown.Snapshot, breakdown.TagSimilarityRaw, breakdown.PopularityRaw,
        breakdown.NoveltyRaw, breakdown.FeedbackPenaltyRaw) { UnknownFields = breakdown.UnknownFields };

    public static WeeklyRecommendationDto ToDto(this WeeklyRecommendation recommendation) => new(
        recommendation.Id,
        recommendation.Rank,
        recommendation.Track.ToDto(),
        recommendation.ScoreBreakdown.ToDto(),
        recommendation.WhyThisPick(),
        recommendation.Feedback,
        recommendation.FeedbackComment,
        recommendation.FeedbackGivenAt,
        recommendation.Rating, recommendation.UserId, recommendation.WeeklyDigestId,
        recommendation.CreatedAt, recommendation.LastModifiedAt, recommendation.CreatedBy,
        recommendation.LastModifiedBy, recommendation.DeletedBy, recommendation.DeletedAt, recommendation.IsDeleted);

    public static WeeklyDigestDto ToDto(this WeeklyDigest digest) => new(
        digest.Id,
        digest.UserId,
        digest.Week.Value,
        digest.WeekStartDate,
        digest.WeekEndDate,
        digest.Status,
        digest.SentAt,
        digest.Recommendations.OrderBy(r => r.Rank).Select(r => r.ToDto()).ToList(),
        digest.CreatedAt, digest.LastModifiedAt, digest.ErrorMessage, digest.CreatedBy,
        digest.LastModifiedBy, digest.DeletedBy, digest.DeletedAt, digest.IsDeleted);

    public static UserMusicConnectionExportDto ToExportDto(this UserMusicConnection connection) => new(
        connection.ServiceType.ToString(),
        connection.ExternalUsername,
        connection.LastSyncedAt,
        connection.IsActive,
        connection.CreatedAt, connection.Id, connection.UserId, connection.LastModifiedAt,
        connection.CreatedBy, connection.LastModifiedBy, connection.DeletedBy, connection.DeletedAt, connection.IsDeleted);
}
