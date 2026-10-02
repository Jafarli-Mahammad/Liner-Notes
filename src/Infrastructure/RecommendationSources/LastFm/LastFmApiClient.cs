using System.Net;
using System.Text.Json;
using LinerNotes.Infrastructure.RecommendationSources.LastFm.Fixtures;
using LinerNotes.Infrastructure.RecommendationSources.LastFm.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinerNotes.Infrastructure.RecommendationSources.LastFm;

/// <summary>
/// Implementation of ILastFmApiClient managing live HTTP communications, rate pacing,
/// in-memory caching, JSON normalization, and hybrid offline fixture fallback.
/// </summary>
public sealed class LastFmApiClient : ILastFmApiClient
{
    private readonly HttpClient _httpClient;
    private readonly LastFmOptions _options;
    private readonly IMemoryCache _cache;
    private readonly LastFmRateLimiter _rateLimiter;
    private readonly ILogger<LastFmApiClient> _logger;

    public LastFmApiClient(
        HttpClient httpClient,
        IOptions<LastFmOptions> options,
        IMemoryCache cache,
        LastFmRateLimiter rateLimiter,
        ILogger<LastFmApiClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<LastFmArtistSummary>> GetSimilarArtistsAsync(
        string artistName,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artistName))
            return Array.Empty<LastFmArtistSummary>();

        if (ShouldUseFixturesDirectly())
            return LastFmFixtureProvider.GetSimilarArtists(artistName).Take(limit).ToList();

        string cacheKey = $"lastfm:artist.getsimilar:{artistName.Trim().ToLowerInvariant()}:{limit}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<LastFmArtistSummary>? cached) && cached is not null)
            return cached;

        try
        {
            var query = new Dictionary<string, string>
            {
                ["method"] = "artist.getsimilar",
                ["artist"] = artistName,
                ["limit"] = limit.ToString(),
                ["autocorrect"] = "1"
            };

            using var doc = await ExecuteRequestAsync(query, cancellationToken).ConfigureAwait(false);
            if (doc is null)
                return FallbackOrEmpty(LastFmFixtureProvider.GetSimilarArtists(artistName).Take(limit).ToList());

            var root = doc.RootElement;
            if (root.TryGetProperty("error", out _))
            {
                _logger.LogWarning("Last.fm returned error for artist.getsimilar (artist: {Artist})", artistName);
                return FallbackOrEmpty(LastFmFixtureProvider.GetSimilarArtists(artistName).Take(limit).ToList());
            }

            var artists = new List<LastFmArtistSummary>();
            if (root.TryGetProperty("similarartists", out var similar) && similar.TryGetProperty("artist", out var artistElem))
            {
                if (artistElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in artistElem.EnumerateArray())
                    {
                        var summary = ParseArtistSummary(item);
                        if (summary is not null) artists.Add(summary);
                    }
                }
                else if (artistElem.ValueKind == JsonValueKind.Object)
                {
                    var summary = ParseArtistSummary(artistElem);
                    if (summary is not null) artists.Add(summary);
                }
            }

            IReadOnlyList<LastFmArtistSummary> result = artists;
            CacheResult(cacheKey, result);
            return result;
        }
        catch (Exception ex) when (IsTransientOrNetwork(ex) && _options.Mode == LastFmClientMode.Hybrid)
        {
            _logger.LogWarning(ex, "Failed to call Last.fm artist.getsimilar for {Artist}; falling back to fixture.", artistName);
            return LastFmFixtureProvider.GetSimilarArtists(artistName).Take(limit).ToList();
        }
    }

    public async Task<IReadOnlyList<LastFmTagItem>> GetArtistTopTagsAsync(
        string artistName,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artistName))
            return Array.Empty<LastFmTagItem>();

        if (ShouldUseFixturesDirectly())
            return LastFmFixtureProvider.GetArtistTopTags(artistName).Take(limit).ToList();

        string cacheKey = $"lastfm:artist.gettoptags:{artistName.Trim().ToLowerInvariant()}:{limit}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<LastFmTagItem>? cached) && cached is not null)
            return cached;

        try
        {
            var query = new Dictionary<string, string>
            {
                ["method"] = "artist.gettoptags",
                ["artist"] = artistName,
                ["autocorrect"] = "1"
            };

            using var doc = await ExecuteRequestAsync(query, cancellationToken).ConfigureAwait(false);
            if (doc is null)
                return FallbackOrEmpty(LastFmFixtureProvider.GetArtistTopTags(artistName).Take(limit).ToList());

            var root = doc.RootElement;
            if (root.TryGetProperty("error", out _))
            {
                _logger.LogWarning("Last.fm returned error for artist.gettoptags (artist: {Artist})", artistName);
                return FallbackOrEmpty(LastFmFixtureProvider.GetArtistTopTags(artistName).Take(limit).ToList());
            }

            var tags = new List<LastFmTagItem>();
            if (root.TryGetProperty("toptags", out var toptags) && toptags.TryGetProperty("tag", out var tagElem))
            {
                if (tagElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in tagElem.EnumerateArray())
                    {
                        var tag = ParseTagItem(item);
                        if (tag is not null) tags.Add(tag);
                        if (tags.Count >= limit) break;
                    }
                }
                else if (tagElem.ValueKind == JsonValueKind.Object)
                {
                    var tag = ParseTagItem(tagElem);
                    if (tag is not null) tags.Add(tag);
                }
            }

            IReadOnlyList<LastFmTagItem> result = tags;
            CacheResult(cacheKey, result);
            return result;
        }
        catch (Exception ex) when (IsTransientOrNetwork(ex) && _options.Mode == LastFmClientMode.Hybrid)
        {
            _logger.LogWarning(ex, "Failed to call Last.fm artist.gettoptags for {Artist}; falling back to fixture.", artistName);
            return LastFmFixtureProvider.GetArtistTopTags(artistName).Take(limit).ToList();
        }
    }

    public async Task<IReadOnlyList<LastFmTrackItem>> GetArtistTopTracksAsync(
        string artistName,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artistName))
            return Array.Empty<LastFmTrackItem>();

        if (ShouldUseFixturesDirectly())
            return LastFmFixtureProvider.GetArtistTopTracks(artistName).Take(limit).ToList();

        string cacheKey = $"lastfm:artist.gettoptracks:{artistName.Trim().ToLowerInvariant()}:{limit}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<LastFmTrackItem>? cached) && cached is not null)
            return cached;

        try
        {
            var query = new Dictionary<string, string>
            {
                ["method"] = "artist.gettoptracks",
                ["artist"] = artistName,
                ["limit"] = limit.ToString(),
                ["autocorrect"] = "1"
            };

            using var doc = await ExecuteRequestAsync(query, cancellationToken).ConfigureAwait(false);
            if (doc is null)
                return FallbackOrEmpty(LastFmFixtureProvider.GetArtistTopTracks(artistName).Take(limit).ToList());

            var root = doc.RootElement;
            if (root.TryGetProperty("error", out _))
            {
                _logger.LogWarning("Last.fm returned error for artist.gettoptracks (artist: {Artist})", artistName);
                return FallbackOrEmpty(LastFmFixtureProvider.GetArtistTopTracks(artistName).Take(limit).ToList());
            }

            var tracks = new List<LastFmTrackItem>();
            if (root.TryGetProperty("toptracks", out var toptracks) && toptracks.TryGetProperty("track", out var trackElem))
            {
                if (trackElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in trackElem.EnumerateArray())
                    {
                        var track = ParseTrackItem(item, artistName);
                        if (track is not null) tracks.Add(track);
                    }
                }
                else if (trackElem.ValueKind == JsonValueKind.Object)
                {
                    var track = ParseTrackItem(trackElem, artistName);
                    if (track is not null) tracks.Add(track);
                }
            }

            IReadOnlyList<LastFmTrackItem> result = tracks;
            CacheResult(cacheKey, result);
            return result;
        }
        catch (Exception ex) when (IsTransientOrNetwork(ex) && _options.Mode == LastFmClientMode.Hybrid)
        {
            _logger.LogWarning(ex, "Failed to call Last.fm artist.gettoptracks for {Artist}; falling back to fixture.", artistName);
            return LastFmFixtureProvider.GetArtistTopTracks(artistName).Take(limit).ToList();
        }
    }

    public async Task<IReadOnlyList<LastFmTrackItem>> GetTagTopTracksAsync(
        string tag,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return Array.Empty<LastFmTrackItem>();

        if (ShouldUseFixturesDirectly())
            return LastFmFixtureProvider.GetTagTopTracks(tag).Take(limit).ToList();

        string cacheKey = $"lastfm:tag.gettoptracks:{tag.Trim().ToLowerInvariant()}:{limit}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<LastFmTrackItem>? cached) && cached is not null)
            return cached;

        try
        {
            var query = new Dictionary<string, string>
            {
                ["method"] = "tag.gettoptracks",
                ["tag"] = tag,
                ["limit"] = limit.ToString()
            };

            using var doc = await ExecuteRequestAsync(query, cancellationToken).ConfigureAwait(false);
            if (doc is null)
                return FallbackOrEmpty(LastFmFixtureProvider.GetTagTopTracks(tag).Take(limit).ToList());

            var root = doc.RootElement;
            if (root.TryGetProperty("error", out _))
            {
                _logger.LogWarning("Last.fm returned error for tag.gettoptracks (tag: {Tag})", tag);
                return FallbackOrEmpty(LastFmFixtureProvider.GetTagTopTracks(tag).Take(limit).ToList());
            }

            var tracks = new List<LastFmTrackItem>();
            if (root.TryGetProperty("tracks", out var trackCollection) && trackCollection.TryGetProperty("track", out var trackElem))
            {
                if (trackElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in trackElem.EnumerateArray())
                    {
                        var track = ParseTrackItem(item, null);
                        if (track is not null) tracks.Add(track);
                    }
                }
                else if (trackElem.ValueKind == JsonValueKind.Object)
                {
                    var track = ParseTrackItem(trackElem, null);
                    if (track is not null) tracks.Add(track);
                }
            }

            IReadOnlyList<LastFmTrackItem> result = tracks;
            CacheResult(cacheKey, result);
            return result;
        }
        catch (Exception ex) when (IsTransientOrNetwork(ex) && _options.Mode == LastFmClientMode.Hybrid)
        {
            _logger.LogWarning(ex, "Failed to call Last.fm tag.gettoptracks for {Tag}; falling back to fixture.", tag);
            return LastFmFixtureProvider.GetTagTopTracks(tag).Take(limit).ToList();
        }
    }

    private async Task<JsonDocument?> ExecuteRequestAsync(
        Dictionary<string, string> queryParams,
        CancellationToken cancellationToken)
    {
        await _rateLimiter.AcquireAsync(cancellationToken).ConfigureAwait(false);

        queryParams["api_key"] = _options.ApiKey ?? string.Empty;
        queryParams["format"] = "json";

        var queryString = string.Join("&", queryParams.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
        string requestUri = $"?{queryString}";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            _logger.LogWarning("Last.fm rate limit reached (HTTP 429).");
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Last.fm returned non-success HTTP status {StatusCode}.", response.StatusCode);
            return null;
        }

        using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private bool ShouldUseFixturesDirectly() =>
        _options.Mode == LastFmClientMode.FixtureOnly ||
        (_options.Mode == LastFmClientMode.Hybrid && string.IsNullOrWhiteSpace(_options.ApiKey));

    private IReadOnlyList<T> FallbackOrEmpty<T>(IReadOnlyList<T> fallbackList) =>
        _options.Mode == LastFmClientMode.Hybrid ? fallbackList : Array.Empty<T>();

    private void CacheResult<T>(string key, T value)
    {
        var cacheEntryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(_options.CacheDurationHours),
            SlidingExpiration = TimeSpan.FromHours(4)
        };
        _cache.Set(key, value, cacheEntryOptions);
    }

    private static bool IsTransientOrNetwork(Exception ex) =>
        ex is HttpRequestException or TimeoutException or TaskCanceledException;

    private static LastFmArtistSummary? ParseArtistSummary(JsonElement element)
    {
        if (!element.TryGetProperty("name", out var nameProp) || string.IsNullOrWhiteSpace(nameProp.GetString()))
            return null;

        string name = nameProp.GetString()!;
        string? mbid = element.TryGetProperty("mbid", out var mbidProp) ? mbidProp.GetString() : null;
        string? match = element.TryGetProperty("match", out var matchProp) ? matchProp.GetString() : null;
        string? url = element.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;

        return new LastFmArtistSummary(name, string.IsNullOrWhiteSpace(mbid) ? null : mbid, match, url);
    }

    private static LastFmTagItem? ParseTagItem(JsonElement element)
    {
        if (!element.TryGetProperty("name", out var nameProp) || string.IsNullOrWhiteSpace(nameProp.GetString()))
            return null;

        string name = nameProp.GetString()!;
        int count = 0;
        if (element.TryGetProperty("count", out var countProp))
        {
            if (countProp.ValueKind == JsonValueKind.Number)
                count = countProp.GetInt32();
            else if (countProp.ValueKind == JsonValueKind.String && int.TryParse(countProp.GetString(), out int parsed))
                count = parsed;
        }

        string? url = element.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
        return new LastFmTagItem(name, count, url);
    }

    private static LastFmTrackItem? ParseTrackItem(JsonElement element, string? fallbackArtist)
    {
        if (!element.TryGetProperty("name", out var nameProp) || string.IsNullOrWhiteSpace(nameProp.GetString()))
            return null;

        string name = nameProp.GetString()!;
        string? mbid = element.TryGetProperty("mbid", out var mbidProp) ? mbidProp.GetString() : null;
        string? url = element.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
        string? duration = element.TryGetProperty("duration", out var durProp) ? durProp.GetString() : null;
        string? listeners = element.TryGetProperty("listeners", out var lisProp) ? lisProp.GetString() : null;
        string? playcount = element.TryGetProperty("playcount", out var playProp) ? playProp.GetString() : null;

        LastFmArtistRef? artist = null;
        if (element.TryGetProperty("artist", out var artElem))
        {
            if (artElem.ValueKind == JsonValueKind.Object)
            {
                string artName = artElem.TryGetProperty("name", out var an) ? an.GetString() ?? "" : "";
                string? artMbid = artElem.TryGetProperty("mbid", out var am) ? am.GetString() : null;
                string? artUrl = artElem.TryGetProperty("url", out var au) ? au.GetString() : null;
                artist = new LastFmArtistRef(artName, artMbid, artUrl);
            }
            else if (artElem.ValueKind == JsonValueKind.String)
            {
                artist = new LastFmArtistRef(artElem.GetString() ?? "", null, null);
            }
        }
        else if (!string.IsNullOrWhiteSpace(fallbackArtist))
        {
            artist = new LastFmArtistRef(fallbackArtist, null, null);
        }

        return new LastFmTrackItem(name, mbid, url, duration, listeners, playcount, artist);
    }
}
