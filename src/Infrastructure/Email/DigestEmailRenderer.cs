using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.DTOs.Digests;
using LinerNotes.Application.DTOs.Subscribers;

namespace LinerNotes.Infrastructure.Email;

/// <summary>Presentation of persisted rank and explanations. No acquisition or scoring.</summary>
public sealed class DigestEmailRenderer(LocalEmailOptions options, IUnsubscribeTokens tokens) : IDigestEmailRenderer
{
    public const string Version = "local-digest-v1";
    public RenderedDigestEmail Render(WeeklyDigestDto digest, SubscriberDto subscriber, string? token = null)
    {
        options.Validate();
        LocalEmailOptions.ValidateAddress(subscriber.Email);
        if (digest.UserId != subscriber.Id || digest.Id == Guid.Empty || digest.Recommendations.Count > 5 ||
            digest.Recommendations.Select(r => r.Rank).Distinct().Count() != digest.Recommendations.Count ||
            digest.Recommendations.Any(r => r.Rank <= 0)) throw new GenerationStoppedException("email_input_invalid");
        token ??= tokens.Create(subscriber.Id);
        if (!tokens.TryRead(token, out var owner) || owner != subscriber.Id) throw new GenerationStoppedException("email_capability_invalid");
        var picks = digest.Recommendations.OrderBy(r => r.Rank).ToArray();
        string origin = options.Origin().GetLeftPart(UriPartial.Authority);
        string unsubscribe = origin + "/api/digests/unsubscribe?token=" + Uri.EscapeDataString(token);
        string account = origin + "/manual-test";
        string title = $"Liner Notes · {digest.Week} · {picks.Length} {(picks.Length == 1 ? "pick" : "picks")}";
        static string H(string value) => WebUtility.HtmlEncode(value);
        var text = new StringBuilder(title).AppendLine().AppendLine();
        var html = new StringBuilder("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width\"><title>")
            .Append(H(title)).Append("</title></head><body style=\"margin:0;background:#f7f4ee;color:#222;font-family:Arial,Helvetica,sans-serif;line-height:1.6\"><main style=\"max-width:620px;margin:0 auto;padding:32px 24px\"><h1 style=\"font-size:26px\">")
            .Append(H(title)).Append("</h1>");
        if (picks.Length == 0)
        {
            const string empty = "No eligible picks were stored for this week. Your saved list is empty.";
            text.AppendLine(empty).AppendLine(); html.Append("<p>").Append(empty).Append("</p>");
        }
        foreach (var pick in picks)
        {
            string query = Uri.EscapeDataString(pick.Track.ArtistName + " " + pick.Track.Title);
            var links = new[] { ("YouTube", "https://www.youtube.com/results?search_query=" + query),
                ("Spotify", "https://open.spotify.com/search/" + query), ("Bandcamp", "https://bandcamp.com/search?q=" + query),
                ("Apple Music", "https://music.apple.com/search?term=" + query) };
            string? spotify = ValidateOptionalLink(pick.Track.ExternalSpotifyUrl);
            string? youtube = ValidateOptionalLink(pick.Track.ExternalYoutubeUrl);
            if (spotify is not null) links[1] = ("Spotify", spotify);
            if (youtube is not null) links[0] = ("YouTube", youtube);
            text.AppendLine($"{pick.Rank}. {pick.Track.Title} — {pick.Track.ArtistName}").AppendLine(pick.WhyThisPick);
            html.Append("<section style=\"border-top:1px solid #d9d3c8;padding:20px 0\"><h2 style=\"font-size:20px;margin:0\">")
                .Append(pick.Rank).Append(". ").Append(H(pick.Track.Title)).Append("</h2><p style=\"margin:4px 0 12px\">")
                .Append(H(pick.Track.ArtistName)).Append("</p><p>").Append(H(pick.WhyThisPick)).Append("</p><p>");
            foreach (var (label, url) in links)
            {
                text.AppendLine(label + ": " + url);
                html.Append("<a style=\"color:#4c4537;margin-right:12px\" href=\"").Append(H(url)).Append("\">").Append(label).Append("</a> ");
            }
            html.Append("</p></section>"); text.AppendLine();
        }
        const string attribution = "Candidate similarity and tags come from Last.fm. Liner Notes applies deterministic scoring; explanations above come from your stored score breakdowns.";
        text.AppendLine(attribution).AppendLine("Inspect your list and data: " + account).AppendLine("Unsubscribe: " + unsubscribe);
        html.Append("<footer style=\"border-top:1px solid #d9d3c8;font-size:14px;padding-top:20px\"><p>").Append(attribution)
            .Append("</p><p><a href=\"").Append(H(account)).Append("\">Inspect your list and data</a></p><p><a href=\"")
            .Append(H(unsubscribe)).Append("\">Unsubscribe from weekly emails</a></p></footer></main></body></html>");
        string plain = text.ToString().Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
        string markup = html.ToString();
        if (Encoding.UTF8.GetByteCount(plain) > options.AlternativeByteLimit || Encoding.UTF8.GetByteCount(markup) > options.AlternativeByteLimit)
            throw new GenerationStoppedException("email_alternative_too_large");
        string fingerprint = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Version, UserId = subscriber.Id, subscriber.Email, DigestId = digest.Id, digest.Week, digest.WeekStartDate, digest.WeekEndDate,
            Picks = picks.Select(p => new { p.Id, p.Rank, p.Track, p.ScoreBreakdown, p.WhyThisPick }),
            Origin = origin, options.FromAddress, options.MessageDomain
        })));
        return new(digest.Id, subscriber.Id, digest.Week, subscriber.Email, Version, fingerprint, token, plain, markup);
    }

    private static string? ValidateOptionalLink(string? value)
    {
        if (value is null) return null;
        if (value.Length > 4096 || value.Any(char.IsControl) || !Uri.TryCreate(value, UriKind.Absolute, out var url) ||
            url.Scheme != "https" || url.UserInfo.Length != 0) throw new GenerationStoppedException("email_link_invalid");
        return url.AbsoluteUri;
    }
}
