using System.Net;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;
using Microsoft.Extensions.Caching.Memory;

namespace LinerNotes.Infrastructure.Artwork;

public sealed class RecordingArtwork(RecordingArtworkCache cache, IListenBrainzApiClient metadata,
    IHttpClientFactory clients, IMetaBrainzRequestGate gate, ListenBrainzOptions options, TimeProvider clock,
    IStoredArtwork? stored = null) : IRecordingArtwork
{
    private const int MaximumBytes = 1024 * 1024;

    public RecordingCover? GetCached(Guid recordingId) => cache.Get(recordingId);

    public RecordingArtworkState Request(Guid recordingId)
    {
        if (recordingId == Guid.Empty || options.Mode != ListenBrainzClientMode.LiveOnly || !options.LiveUseApproved) return RecordingArtworkState.Unavailable;
        if (cache.Contains(recordingId)) return cache.Get(recordingId) is not null ? RecordingArtworkState.Ready
            : cache.Retryable(recordingId) ? RecordingArtworkState.Pending : RecordingArtworkState.Unavailable;
        cache.Enqueue([recordingId], priority: true);
        return RecordingArtworkState.Pending;
    }

    public async Task WarmAsync(IReadOnlyList<Guid> recordingIds, CancellationToken ct, bool allowAcquisition = true)
    {
        if (options.Mode != ListenBrainzClientMode.LiveOnly || !options.LiveUseApproved) return;
        foreach (var batch in recordingIds.Where(id => id != Guid.Empty).Distinct().Where(id => !cache.Contains(id)).Chunk(50))
        {
            var missing = new List<Guid>();
            foreach (var id in batch)
            {
                var retained = stored is null ? null : await stored.FindAsync(id, ct);
                if (retained is null) missing.Add(id);
                else Remember(id, retained);
            }
            if (missing.Count == 0 || !allowAcquisition) continue;
            {
                var response = await metadata.SendAsync(new(ListenBrainzRequestKind.ArtworkMetadata,
                    string.Join(',', missing.Select(id => id.ToString("D")))), ct);
                await Parallel.ForEachAsync(missing, new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = ct }, async (id, token) =>
                {
                    RecordingCover? cover = null;
                    bool retryable = response.Gaps.Any(g => g.Reason is "rate_limit" or "transport_error" or "upstream_unavailable");
                    if (response.Data is { ValueKind: JsonValueKind.Object } data &&
                        data.TryGetProperty(id.ToString("D"), out var item) &&
                        item.ValueKind == JsonValueKind.Object && item.TryGetProperty("release", out var release))
                    {
                        var releaseId = Id(release, "mbid");
                        var caaId = Id(release, "caa_release_mbid");
                        var groupId = Id(release, "release_group_mbid");
                        if (releaseId is { } rid) await Fetch("release", rid);
                        if (cover is null && caaId is { } cid && caaId != releaseId) await Fetch("release", cid);
                        if (cover is null && groupId is { } gid) await Fetch("release-group", gid);
                    }
                    if (cover is not null)
                    {
                        var now = clock.GetUtcNow();
                        var mappingExpiry = response.SharedCacheUntilUtc;
                        if (mappingExpiry is null || mappingExpiry <= now)
                            cover = cover with { CanPersist = false, ExpiresAtUtc = now.AddSeconds(1) };
                        else if (cover.ExpiresAtUtc > mappingExpiry) cover = cover with { ExpiresAtUtc = mappingExpiry };
                    }
                    Remember(id, cover, retryable);
                    async Task Fetch(string kind, Guid identity)
                    {
                        var result = await FetchAsync(kind, identity, token);
                        cover = result.Cover; retryable |= result.Retryable;
                    }
                });
                // The scoped EF store is intentionally used sequentially after network work.
                if (stored is not null)
                    foreach (var id in missing)
                        if (cache.Get(id) is { ExpiresAtUtc: not null, CanPersist: true } cover) await stored.SaveAsync(id, cover, ct);
            }
        }
    }

    private void Remember(Guid id, RecordingCover? cover, bool retryable = false) => cache.Entries.Set("recording:" + id,
        new ArtworkCacheEntry(cover, cover is null && retryable), new MemoryCacheEntryOptions
    {
        Size = (cover?.Bytes.Length ?? 0) + 128,
        AbsoluteExpirationRelativeToNow = cover?.ExpiresAtUtc is { } expiry
            ? expiry > clock.GetUtcNow() ? expiry - clock.GetUtcNow() : TimeSpan.FromSeconds(1)
            : cover is null ? TimeSpan.FromMinutes(1) : TimeSpan.FromDays(1)
    });

    private static Guid? Id(JsonElement element, string property) => element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String &&
        Guid.TryParseExact(value.GetString(), "D", out var id) && id != Guid.Empty ? id : null;

    private async Task<ArtworkCacheEntry> FetchAsync(string kind, Guid id, CancellationToken ct)
    {
        string key = kind + ":" + id;
        var stripe = cache.Gates[(uint)key.GetHashCode() % (uint)cache.Gates.Length];
        await stripe.WaitAsync(ct);
        try { return await FetchCoreAsync(key, kind, id, ct); }
        finally { stripe.Release(); }
    }

    private async Task<ArtworkCacheEntry> FetchCoreAsync(string key, string kind, Guid id, CancellationToken ct)
    {
        if (cache.Entries.TryGetValue(key, out ArtworkCacheEntry? cached) && cached is not null) return cached;
        RecordingCover? cover = null;
        bool retryable = true;
        var lifetime = TimeSpan.FromMinutes(1);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            var token = timeout.Token;
            using var http = clients.CreateClient("CoverArtArchive");
            var uri = new Uri($"https://coverartarchive.org/{kind}/{id:D}/front-500");
            for (int hop = 0; hop < 6; hop++)
            {
                await gate.WaitAsync("CoverArtArchive", token);
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.UserAgent.ParseAdd(options.UserAgent);
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
                {
                    var delay = response.Headers.RetryAfter?.Delta ??
                        (response.Headers.RetryAfter?.Date - clock.GetUtcNow()) ?? TimeSpan.FromSeconds(30);
                    delay = TimeSpan.FromSeconds(Math.Clamp(delay.TotalSeconds, 1, 86400));
                    await gate.DeferAsync("CoverArtArchive", delay, token);
                    lifetime = delay;
                    break;
                }
                if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
                {
                    if (response.Headers.Location is not { } location) break;
                    var next = new Uri(uri, location);
                    // CAA still documents HTTP redirects; upgrade known archive hosts to HTTPS.
                    if (!Allowed(next)) break;
                    uri = new UriBuilder(next) { Scheme = "https", Port = -1 }.Uri;
                    continue;
                }
                if (response.StatusCode == HttpStatusCode.NotFound) { lifetime = TimeSpan.FromHours(6); retryable = false; break; }
                if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaximumBytes) break;
                string? type = response.Content.Headers.ContentType?.MediaType;
                if (type is not ("image/jpeg" or "image/png" or "image/webp")) break;
                await using var input = await response.Content.ReadAsStreamAsync(token);
                using var output = new MemoryStream();
                var buffer = new byte[8192];
                int read;
                while ((read = await input.ReadAsync(buffer, token)) > 0)
                {
                    if (output.Length + read > MaximumBytes) throw new InvalidDataException("artwork_too_large");
                    output.Write(buffer, 0, read);
                }
                var bytes = output.ToArray();
                if (!ValidImage(bytes, type)) break;
                lifetime = TimeSpan.FromDays(1);
                var control = response.Headers.CacheControl;
                if (control?.SharedMaxAge is { } shared && shared < lifetime) lifetime = shared;
                else if (control?.MaxAge is { } age && age < lifetime) lifetime = age;
                var apparentAge = clock.GetUtcNow() - (response.Headers.Date ?? clock.GetUtcNow());
                if (apparentAge < TimeSpan.Zero) apparentAge = TimeSpan.Zero;
                lifetime -= response.Headers.Age is { } suppliedAge && suppliedAge > apparentAge ? suppliedAge : apparentAge;
                if (control?.SharedMaxAge is null && control?.MaxAge is null && response.Content.Headers.Expires is { } suppliedExpiry && suppliedExpiry - clock.GetUtcNow() < lifetime)
                    lifetime = suppliedExpiry - clock.GetUtcNow();
                bool reusable = control is not ({ NoStore: true } or { NoCache: true } or { Private: true }) && lifetime > TimeSpan.Zero;
                if (!reusable) lifetime = TimeSpan.FromSeconds(1);
                var until = clock.GetUtcNow() + lifetime;
                var expiry = new DateTimeOffset(until.Ticks - until.Ticks % 10, TimeSpan.Zero);
                cover = new(bytes, type, expiry, reusable);
                break;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or OperationCanceledException or
            LinerNotes.Application.Common.Interfaces.Recommendation.GenerationStoppedException) { }
        var entry = new ArtworkCacheEntry(cover, cover is null && retryable);
        cache.Entries.Set(key, entry, new MemoryCacheEntryOptions
        {
            Size = (cover?.Bytes.Length ?? 0) + 128, AbsoluteExpirationRelativeToNow = lifetime
        });
        return entry;
    }

    private static bool Allowed(Uri uri) => uri.Scheme is "http" or "https" && uri.IsDefaultPort &&
        string.IsNullOrEmpty(uri.UserInfo) &&
        (uri.Host == "coverartarchive.org" || uri.Host == "archive.org" || uri.Host.EndsWith(".archive.org", StringComparison.Ordinal));

    internal static bool ValidImage(byte[] bytes, string type) => bytes.Length <= MaximumBytes && type switch
    {
        "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff,
        "image/png" => bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        "image/webp" => bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
        _ => false
    };
}
