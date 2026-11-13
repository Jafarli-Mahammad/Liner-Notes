using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;

namespace LinerNotes.Domain.Taste;

// Reconstructed from authoritative feedback; contains neither written notes nor score receipts.
public sealed record FeedbackObservation(Guid Id, Guid ProfileId, Guid? UserId, string Artist,
    string Title, string? Mbid, UserFeedback Feedback, int? Rating, DateTime ChangedAt)
{
    public string ArtistKey => Artist.Trim().ToLowerInvariant();
    public IReadOnlyList<string> Aliases => string.IsNullOrWhiteSpace(Mbid)
        ? [ArtistKey + ":" + Title.Trim().ToLowerInvariant()]
        : ["mbid:" + Mbid.Trim().ToLowerInvariant(), ArtistKey + ":" + Title.Trim().ToLowerInvariant()];
    public static FeedbackObservation From(WeeklyRecommendation r) => new(r.Id, r.ProfileId, r.UserId,
        r.Track.ArtistName, r.Track.Title, r.Track.Mbid, r.Feedback, r.Rating,
        r.FeedbackGivenAt ?? r.CreatedAt);
}

public sealed record FeedbackArtistSupport(string Artist, int PositiveRecordings, int NegativeRecordings,
    int RecentPositiveRecordings, double LearnedWeight, bool Attenuated);
