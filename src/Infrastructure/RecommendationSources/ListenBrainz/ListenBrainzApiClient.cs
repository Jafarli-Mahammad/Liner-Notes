using LinerNotes.Infrastructure.Caching;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using Microsoft.Extensions.Caching.Memory;

namespace LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;

/// <summary>Only fixed, allowlisted public metadata endpoints. Tokens never reach MusicBrainz.</summary>
public sealed class ListenBrainzApiClient(HttpClient http, ListenBrainzOptions options,
    IMetaBrainzRequestGate gate, IMemoryCache cache, TimeProvider clock, DiscoveryMemoryCaches? memoryCaches = null) : IListenBrainzApiClient
{
    private int sentRequests;
    private readonly IMemoryCache responseCache = memoryCaches?.Metadata ?? cache;
    public async Task<ListenBrainzResponse> SendAsync(ListenBrainzRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        options.Validate(); request.Validate();
        if (options.Mode != ListenBrainzClientMode.LiveOnly || !options.LiveUseApproved)
            return ListenBrainzResponse.Gap(request, EvidenceOrigin.Live, "listenbrainz_live_use_unapproved");
        string root = request.Kind == ListenBrainzRequestKind.SimilarRecordings ? "https://labs.api.listenbrainz.org/" :
            request.Provider == "MusicBrainz" ? "https://musicbrainz.org/" : "https://api.listenbrainz.org/";
        string cacheKey = "metabrainz-public-v1:" + root + request.Key;
        if (responseCache.TryGetValue(cacheKey, out ListenBrainzResponse? hit) && hit is not null) return hit;
        try
        {
            if (Interlocked.Increment(ref sentRequests) > options.MaximumRequests)
                throw new GenerationStoppedException("upstream_request_limit");
            await gate.WaitAsync(request.Provider, cancellationToken).ConfigureAwait(false);
            using var message = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(root), request.RelativeUri()));
            message.Headers.UserAgent.ParseAdd(options.UserAgent);
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (request.Provider == "ListenBrainz" && request.Kind != ListenBrainzRequestKind.SimilarRecordings && !string.IsNullOrEmpty(options.Token))
                message.Headers.Authorization = new AuthenticationHeaderValue("Token", options.Token);
            using var response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            TimeSpan backoff = response.Headers.RetryAfter?.Delta ??
                (response.Headers.RetryAfter?.Date is { } date ? date - clock.GetUtcNow() : TimeSpan.Zero);
            if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining) && remaining.FirstOrDefault() == "0" &&
                response.Headers.TryGetValues("X-RateLimit-Reset-In", out var reset) &&
                double.TryParse(reset.FirstOrDefault(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && double.IsFinite(seconds) && seconds > 0)
                backoff = TimeSpan.FromSeconds(Math.Min(seconds, 86400)) > backoff ? TimeSpan.FromSeconds(Math.Min(seconds, 86400)) : backoff;
            if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable && backoff < TimeSpan.FromSeconds(30))
                backoff = TimeSpan.FromSeconds(30);
            if (backoff > TimeSpan.Zero) await gate.DeferAsync(request.Provider, backoff > TimeSpan.FromDays(1) ? TimeSpan.FromDays(1) : backoff, cancellationToken).ConfigureAwait(false);
            if (response.Content.Headers.ContentLength > ListenBrainzResponse.MaximumBytes)
                return ListenBrainzResponse.Gap(request, EvidenceOrigin.Live, "response_size_limit");
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var output = new MemoryStream();
            byte[] buffer = new byte[8192];
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (output.Length + read > ListenBrainzResponse.MaximumBytes)
                    return ListenBrainzResponse.Gap(request, EvidenceOrigin.Live, "response_size_limit");
                output.Write(buffer, 0, read);
            }
            var parsed = ListenBrainzResponse.Parse(request, output.ToArray(), EvidenceOrigin.Live, clock.GetUtcNow());
            if (!response.IsSuccessStatusCode)
                return ListenBrainzResponse.Gap(request, EvidenceOrigin.Live,
                    response.StatusCode == HttpStatusCode.TooManyRequests ? "rate_limit" :
                    response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? "permission_denied" :
                    response.StatusCode == HttpStatusCode.NotFound ? "not_found" :
                    (int)response.StatusCode >= 500 ? "upstream_unavailable" : "http_error", parsed.Reference);
            // No authenticated private endpoints are exposed by this client. Still obey no-store/no-cache.
            if (parsed.Gaps.IsEmpty && response.Headers.CacheControl is not { NoStore: true } and not { NoCache: true } and not { Private: true })
            {
                TimeSpan ttl = TimeSpan.FromSeconds(options.CacheSeconds);
                if (response.Headers.CacheControl?.MaxAge is { } supplied && supplied < ttl) ttl = supplied;
                if (response.Headers.CacheControl?.MaxAge is null && response.Content.Headers.Expires is { } expires)
                {
                    var lifetime = expires - (response.Headers.Date ?? clock.GetUtcNow());
                    if (lifetime < ttl) ttl = lifetime;
                }
                var apparentAge = clock.GetUtcNow() - (response.Headers.Date ?? clock.GetUtcNow());
                if (apparentAge < TimeSpan.Zero) apparentAge = TimeSpan.Zero;
                var age = response.Headers.Age ?? TimeSpan.Zero;
                ttl -= apparentAge > age ? apparentAge : age;
                if (ttl > TimeSpan.Zero)
                {
                    parsed = parsed with { SharedCacheUntilUtc = clock.GetUtcNow() + ttl };
                    responseCache.Set(cacheKey, parsed, new MemoryCacheEntryOptions { Size = DiscoveryMemoryCaches.EstimateBytes(output.Length, cacheKey), AbsoluteExpirationRelativeToNow = ttl });
                }
            }
            return parsed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException)
        {
            // URLs and headers can appear in exception messages; never log them.
            return ListenBrainzResponse.Gap(request, EvidenceOrigin.Live, "transport_error");
        }
    }
}
