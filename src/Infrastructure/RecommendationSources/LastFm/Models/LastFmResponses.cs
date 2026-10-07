using System.Text.Json.Serialization;

namespace LinerNotes.Infrastructure.RecommendationSources.LastFm.Models;

public sealed record LastFmArtistSummary(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("mbid")] string? Mbid,
    [property: JsonPropertyName("match")] string? Match,
    [property: JsonPropertyName("url")] string? Url,
    int? ResponsePosition = null);

public sealed record LastFmTagItem(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("count")] long? Count,
    [property: JsonPropertyName("url")] string? Url,
    string? RawCount = null,
    int? ResponsePosition = null);

public sealed record LastFmArtistRef(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("mbid")] string? Mbid,
    [property: JsonPropertyName("url")] string? Url);

public sealed record LastFmTrackItem(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("mbid")] string? Mbid,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("duration")] string? Duration,
    [property: JsonPropertyName("listeners")] string? Listeners,
    [property: JsonPropertyName("playcount")] string? Playcount,
    [property: JsonPropertyName("artist")] LastFmArtistRef? Artist,
    string? RawRank = null,
    int? ResponsePosition = null);

public sealed record LastFmErrorResponse(
    [property: JsonPropertyName("error")] int ErrorCode,
    [property: JsonPropertyName("message")] string Message);
