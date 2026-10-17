using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinerNotes.Application.Common.Models.Recommendation;

namespace LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;

/// <summary>Explicitly synthetic mechanics only. No real catalog or similarity claim.</summary>
public sealed class FixtureListenBrainzApiClient : IListenBrainzApiClient
{
    private readonly Dictionary<string, string> artistNames = new(StringComparer.Ordinal);
    internal static string Id(string text) => new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("listenbrainz-synthetic:" + text)).AsSpan(0, 16)).ToString("D");
    private static string Artist(int i) => Id("artist-" + i);
    private static string Recording(int i) => Id("recording-" + i);

    public Task<ListenBrainzResponse> SendAsync(ListenBrainzRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); request.Validate();
        object data;
        switch (request.Kind)
        {
            case ListenBrainzRequestKind.ArtistSearch:
                string id = Id(request.Identity.ToLowerInvariant());
                artistNames[id] = request.Identity;
                data = new { count = 1, artists = new[] { new { id, name = request.Identity } } };
                break;
            case ListenBrainzRequestKind.AlbumSearch:
                data = new Dictionary<string, object>
                {
                    ["release-groups"] = new[] { new Dictionary<string, object>
                    {
                        ["id"] = Id("album:" + request.Identity), ["title"] = request.Identity,
                        ["disambiguation"] = "Synthetic album fixture", ["first-release-date"] = "2000",
                        ["artist-credit"] = new[] { new Dictionary<string, object> { ["artist"] = new { id = Id("album-artist:" + request.Identity), name = "Synthetic album artist" } } }
                    } }
                };
                break;
            case ListenBrainzRequestKind.ArtistRadio:
                data = Enumerable.Range(1, Math.Min(request.Limit, 5)).ToDictionary(Artist,
                    i => new[] { new { recording_mbid = Recording(i), similar_artist_mbid = Artist(i), similar_artist_name = "Synthetic Ensemble " + i } });
                break;
            case ListenBrainzRequestKind.TagRadio:
                data = Enumerable.Range(1, Math.Min(request.Limit, 5)).Select(i => new { recording_mbid = Recording(i) }).ToArray();
                break;
            case ListenBrainzRequestKind.RecordingMetadata:
                data = Enumerable.Range(1, 5).Where(i => request.Identity.Split(',').Contains(Recording(i))).ToDictionary(Recording,
                    i => new { recording = new { name = "Synthetic Discovery " + i }, artist = new { name = "Synthetic Ensemble " + i,
                        artists = new[] { new { artist_mbid = Artist(i), name = "Synthetic Ensemble " + i } } } });
                break;
            case ListenBrainzRequestKind.AlbumMetadata:
                data = new Dictionary<string, object> { [request.Identity] = new {
                    tag = new { release_group = new[] { new { tag = "electronic", count = 10 }, new { tag = "ambient", count = 5 } } }
                }};
                break;
            case ListenBrainzRequestKind.ArtistMetadata:
                data = request.Identity.Split(',').Select(artist => new { artist_mbid = artist, name = artistNames.GetValueOrDefault(artist, "Synthetic Ensemble"),
                    tag = new { artist = new[] { "rock", "electronic", "synthpop", "alternative" }
                        .Select((tag, index) => new { artist_mbid = artist, tag, count = 10 - index }).ToArray() } }).ToArray();
                break;
            default: throw new ArgumentException("listenbrainz_request_invalid");
        }
        return Task.FromResult(ListenBrainzResponse.Parse(request, JsonSerializer.SerializeToUtf8Bytes(data), EvidenceOrigin.Synthetic, DateTimeOffset.UnixEpoch));
    }
}
