using LinerNotes.Domain.Digest;
using Microsoft.EntityFrameworkCore;

namespace LinerNotes.DataAccess.Persistence.Repositories;

internal static class RecentArtistHistoryQuery
{
    internal static async Task<IReadOnlyDictionary<string, int>> ReadAsync(IQueryable<WeeklyRecommendation> owned, CancellationToken ct)
    {
        // Stream the bounded window; do not retain forty expanded selection receipts.
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var recent = owned.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Take(40)
            .Select(r => new { r.Track.NormalizedArtistName, r.ScoreBreakdown }).AsAsyncEnumerable();
        await foreach (var row in recent.WithCancellation(ct))
        {
            var ids = row.ScoreBreakdown.Snapshot?.Evidence.CreditedArtistIds ?? [];
            var keys = ids.Count == 0 ? ["name:" + row.NormalizedArtistName]
                : ids.Distinct().Select(id => "artist:" + id.ToString("D")).ToArray();
            foreach (string key in keys) counts[key] = counts.GetValueOrDefault(key) + 1;
        }
        return counts;
    }
}
