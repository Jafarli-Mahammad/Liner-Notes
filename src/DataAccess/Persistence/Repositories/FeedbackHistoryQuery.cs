using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Taste;
using Microsoft.EntityFrameworkCore;

namespace LinerNotes.DataAccess.Persistence.Repositories;

internal static class FeedbackHistoryQuery
{
    internal static async Task<(WeeklyRecommendation[] Recent, FeedbackObservation[] History)> ReadAsync(
        IQueryable<WeeklyRecommendation> owned, int window, CancellationToken ct)
    {
        var musical = owned.Where(r => r.FeedbackGivenAt != null);
        // Lifetime history carries only scalar votes. Never decode lifetime score receipts or notes.
        var history = await musical.Select(r => new FeedbackObservation(r.Id, r.ProfileId, r.UserId,
            r.Track.ArtistName, r.Track.Title, r.Track.Mbid, r.Feedback, r.Rating,
            r.FeedbackGivenAt!.Value)).ToArrayAsync(ct);
        var recent = await musical.Where(r => r.Feedback != LinerNotes.Domain.Enums.UserFeedback.None || r.Rating != null).Include(r => r.Track).OrderByDescending(r => r.FeedbackGivenAt)
            .ThenByDescending(r => r.Id).Take(window).ToArrayAsync(ct);
        return (recent, history);
    }
}
