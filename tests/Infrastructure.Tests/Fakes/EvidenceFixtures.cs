using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Infrastructure.RecommendationSources.LastFm;
using LinerNotes.Infrastructure.RecommendationSources.LastFm.Models;

namespace LinerNotes.Infrastructure.Tests.Fakes;

public static class EvidenceFixtures
{
    public static LastFmResponse<T> Response<T>(IEnumerable<T> items, string identity = "fixture",
        EvidenceOrigin origin = EvidenceOrigin.Synthetic, string? method = null)
    {
        var data = items.ToArray();
        method ??= typeof(T) == typeof(LastFmTagItem) ? "artist.gettoptags" :
            typeof(T) == typeof(LastFmArtistSummary) ? "artist.getsimilar" : "artist.gettoptracks";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(data);
        return new(data, new("Last.fm", method, identity, origin, Convert.ToHexStringLower(SHA256.HashData(bytes)), DateTimeOffset.UnixEpoch));
    }

    public static RawCandidateTrack Raw(string title, string artist, string? mbid = null, double? match = .9,
        string? listeners = "25000", EvidenceOrigin origin = EvidenceOrigin.Synthetic)
    {
        string? rawMatch = match?.ToString(CultureInfo.InvariantCulture);
        var similar = Response([new LastFmArtistSummary(artist, null, rawMatch, null)], "seed", origin);
        var tracks = Response([new LastFmTrackItem(title, mbid, null, null, listeners, null, new(artist, null, null))], artist, origin);
        return new(title, artist, mbid, [new("seed", LastFmEvidenceParser.Match(rawMatch), similar.Reference,
            tracks.Reference, 1, null, null, LastFmEvidenceParser.Count(listeners))]);
    }
}
