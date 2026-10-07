using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Infrastructure.RecommendationSources.LastFm.Fixtures;
using LinerNotes.Infrastructure.RecommendationSources.LastFm.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinerNotes.Infrastructure.RecommendationSources.LastFm;

/// <summary>Explicit fixture or HTTP ingestion; real failures never become synthetic evidence.</summary>
public sealed class LastFmApiClient : ILastFmApiClient
{
    private readonly HttpClient http;
    private readonly LastFmOptions options;
    private readonly IMemoryCache cache;
    private readonly LastFmRateLimiter limiter;
    private readonly ILogger<LastFmApiClient> logger;
    private const int MaximumResponseBytes = 100_000;

    public LastFmApiClient(HttpClient httpClient, IOptions<LastFmOptions> options,
        IMemoryCache cache, LastFmRateLimiter rateLimiter, ILogger<LastFmApiClient> logger)
    {
        http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        limiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<LastFmResponse<LastFmArtistSummary>> GetSimilarArtistsAsync(string artistName, int limit = 10, CancellationToken cancellationToken = default) =>
        GetAsync("artist.getsimilar", "artist", artistName, limit, LastFmFixtureProvider.GetSimilarArtists, "similarartists", "artist", cancellationToken);
    public Task<LastFmResponse<LastFmTagItem>> GetArtistTopTagsAsync(string artistName, int limit = 20, CancellationToken cancellationToken = default) =>
        GetAsync("artist.gettoptags", "artist", artistName, limit, LastFmFixtureProvider.GetArtistTopTags, "toptags", "tag", cancellationToken);
    public Task<LastFmResponse<LastFmTrackItem>> GetArtistTopTracksAsync(string artistName, int limit = 5, CancellationToken cancellationToken = default) =>
        GetAsync("artist.gettoptracks", "artist", artistName, limit, LastFmFixtureProvider.GetArtistTopTracks, "toptracks", "track", cancellationToken);
    public Task<LastFmResponse<LastFmTrackItem>> GetTagTopTracksAsync(string tag, int limit = 10, CancellationToken cancellationToken = default) =>
        GetAsync("tag.gettoptracks", "tag", tag, limit, LastFmFixtureProvider.GetTagTopTracks, "tracks", "track", cancellationToken);

    private async Task<LastFmResponse<T>> GetAsync<T>(string method, string parameter, string identity, int limit,
        Func<string, IReadOnlyList<T>> fixture, string container, string item, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));
        identity = identity?.Trim() ?? "";
        var origin = options.Mode == LastFmClientMode.FixtureOnly ? EvidenceOrigin.Synthetic : EvidenceOrigin.Live;
        ResponseReference FailureReference() => new("Last.fm", method, identity, origin,
            null, null); // No received response: deliberately no invented hash/time.
        if (identity.Length == 0) return LastFmEvidenceParser.Gap<T>(FailureReference(), "invalid_seed");
        if (options.Mode == LastFmClientMode.FixtureOnly)
        {
            var list = fixture(identity).Take(limit).ToArray();
            // Hash the exact synthetic JSON payload, with an explicitly synthetic fixed timestamp.
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
                { [container] = new Dictionary<string, object> { [item] = list } });
            var reference = new ResponseReference("Last.fm", method, identity, EvidenceOrigin.Synthetic,
                Convert.ToHexStringLower(SHA256.HashData(bytes)), DateTimeOffset.UnixEpoch);
            return new(list, reference, list.Length == 0 ? [new(identity, method, "unknown_fixture_seed", reference)] : []);
        }
        if (string.IsNullOrWhiteSpace(options.ApiKey)) return LastFmEvidenceParser.Gap<T>(FailureReference(), "missing_credentials");
        if (options.Mode is not (LastFmClientMode.LiveOnly or LastFmClientMode.Hybrid))
            return LastFmEvidenceParser.Gap<T>(FailureReference(), "invalid_mode");
        // Includes credential fingerprint without exposing it; modes/limits cannot share envelopes.
        string cacheKey = $"lastfm-evidence-v1:{options.Mode}:{http.BaseAddress}:{Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(options.ApiKey)))}:{method}:{identity.ToLowerInvariant()}:{limit}";
        if (cache.TryGetValue(cacheKey, out LastFmResponse<T>? hit) && hit is not null) return hit;
        await limiter.AcquireAsync(cancellationToken).ConfigureAwait(false);
        var query = new Dictionary<string, string> { ["method"] = method, [parameter] = identity,
            ["api_key"] = options.ApiKey, ["format"] = "json" };
        if (parameter == "artist") query["autocorrect"] = "0";
        if (method != "artist.gettoptags") query["limit"] = limit.ToString(CultureInfo.InvariantCulture);
        string url = "?" + string.Join("&", query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var memory = new MemoryStream();
            var buffer = new byte[8192];
            int count;
            while ((count = await content.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, MaximumResponseBytes + 1 - (int)memory.Length)), cancellationToken).ConfigureAwait(false)) > 0)
            {
                memory.Write(buffer, 0, count);
                if (memory.Length > MaximumResponseBytes) return LastFmEvidenceParser.Gap<T>(FailureReference(), "oversized_response");
            }
            byte[] bytes = memory.ToArray();
            var reference = new ResponseReference("Last.fm", method, identity, EvidenceOrigin.Live,
                Convert.ToHexStringLower(SHA256.HashData(bytes)), DateTimeOffset.UtcNow);
            if (!response.IsSuccessStatusCode)
                return LastFmEvidenceParser.Gap<T>(reference, response.StatusCode == HttpStatusCode.TooManyRequests ? "rate_limit" : "http_error");
            var parsed = LastFmEvidenceParser.Parse<T>(method, identity, bytes, EvidenceOrigin.Live, reference.RetrievedAtUtc!.Value, limit);
            // Cache only within supplied HTTP freshness. No header means no adopted cache lifetime.
            var control = response.Headers.CacheControl;
            var apparentAge = DateTimeOffset.UtcNow - (response.Headers.Date ?? reference.RetrievedAtUtc!.Value);
            if (apparentAge < TimeSpan.Zero) apparentAge = TimeSpan.Zero;
            var age = response.Headers.Age ?? TimeSpan.Zero;
            if (age < TimeSpan.Zero) age = TimeSpan.Zero;
            var ttl = control?.MaxAge ?? (response.Content.Headers.Expires - (response.Headers.Date ?? reference.RetrievedAtUtc!.Value));
            if (ttl.HasValue) ttl -= apparentAge > age ? apparentAge : age;
            if (control?.NoStore != true && control?.NoCache != true && ttl is { } supplied && supplied > TimeSpan.Zero && parsed.Gaps.IsEmpty)
                cache.Set(cacheKey, parsed, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = supplied, Size = 1 });
            return parsed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is HttpRequestException or IOException or OperationCanceledException)
        {
            // Exception messages may contain URLs/api_key: never log the exception or URI.
            logger.LogWarning("Last.fm ingestion stopped with a transport gap.");
            return LastFmEvidenceParser.Gap<T>(FailureReference(), "transport_error");
        }
    }
}
