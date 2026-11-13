using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.Application.Common.Mappings;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Email;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Scoring;
namespace LinerNotes.Application.Services;

public sealed class WeeklyEmailQueue(IWeeklyDigestRepository digests, IUserRepository users, IDigestEmailRenderer renderer,
    EmailQueue queue, EmailDeliveryOptions options)
{
    public async Task<bool> EnqueueAsync(Guid userId, IsoWeek week, CancellationToken ct)
    {
        if (!queue.Enabled) return false;
        var digest = await digests.GetByUserIdAndWeekAsync(userId, week, ct);
        var user = await users.GetByIdAsync(userId, ct);
        if (digest is null || user is null || user.IsDeleted || user.EmailUnsubscribedAtUtc is not null || !user.WeeklyEmailOptIn) return false;
        if (digest.Status == DigestStatus.Sent) return true;
        if (digest.Status == DigestStatus.LocalCaptured) return false;
        var dto = digest.ToDto();
        foreach (var pick in dto.Recommendations)
        {
            if (pick.ScoreBreakdown.Snapshot is not { } snapshot) return false;
            var evidence = snapshot.Evidence;
            IEnumerable<StoredResponse> responses = evidence.SupportingResponses
                .Concat(evidence.Seeds.SelectMany(s => s.SupportingResponses.Append(s.Response)))
                .Concat(evidence.DiscoveryPaths.SelectMany(p => p.SupportingResponses.Append(p.TrackResponse)
                    .Concat(p.SimilarityResponse is null ? [] : new[] { p.SimilarityResponse })))
                .Concat(evidence.CandidateTags.Select(t => t.Response));
            if (evidence.CandidateTagResponse is { } tagResponse) responses = responses.Append(tagResponse);
            var sourceResponses = responses.ToArray();
            if (sourceResponses.Length == 0 || sourceResponses.Any(r => r.Origin != "Live" || r.Provider is not ("ListenBrainz" or "MusicBrainz"))) return false;
        }
        var message = renderer.Render(dto, user.ToDto());
        var unsubscribe = options.PublicOrigin + "/api/digests/unsubscribe?token=" + Uri.EscapeDataString(message.Token);
        var headers = new Dictionary<string, string> { ["List-Unsubscribe"] = "<" + unsubscribe + ">", ["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click" };
        var text = message.PlainText + "\nSources and rights: " + options.PublicOrigin + "/sources\n";
        var html = message.Html.Replace("</main>", "<p><a href=\"" + options.PublicOrigin + "/sources\">Music sources and rights</a></p></main>", StringComparison.Ordinal);
        return await queue.EnqueueAsync(userId, digest.Id, EmailPurpose.WeeklyDigest, "weekly:" + digest.Id.ToString("N"),
            new(message.Recipient, "Liner Notes · " + message.Week, text, html, headers, [unsubscribe]), TimeSpan.FromDays(7), TimeSpan.Zero, ct);
    }
}
