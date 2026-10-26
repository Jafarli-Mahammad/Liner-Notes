using System.Net;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;
using Microsoft.Extensions.Caching.Memory;

namespace LinerNotes.Infrastructure.Artwork;

public sealed class RecordingArtwork(RecordingArtworkCache cache, IListenBrainzApiClient metadata,
    IHttpClientFactory clients, IMetaBrainzRequestGate gate, ListenBrainzOptions options, TimeProvider clock) : IRecordingArtwork
{
    private const int MaximumBytes = 1024 * 1024;

    public RecordingCover? GetCached(Guid recordingId) => cache.Get(recordingId);

    public async Task WarmAsync(IReadOnlyList<Guid> recordingIds, CancellationToken ct)
    {
        if (options.Mode != ListenBrainzClientMode.LiveOnly || !options.LiveUseApproved) return;
        await cache.Gate.WaitAsync(ct);
        try
        {
            foreach (var batch in recordingIds.Where(id => id != Guid.Empty).Distinct()
                         .Where(id => !cache.Entries.TryGetValue("recording:" + id, out _)).Chunk(50))
            {
                var response = await metadata.SendAsync(new(ListenBrainzRequestKind.ArtworkMetadata,
                    string.Join(',', batch.Select(id => id.ToString("D")))), ct);
                foreach (var id in batch)
                {
                    RecordingCover? cover = null;
                    var lifetime = TimeSpan.FromMinutes(1);
                    if (response.Data is { ValueKind: JsonValueKind.Object } data &&
                        data.TryGetProperty(id.ToString("D"), out var item) &&
                        item.ValueKind == JsonValueKind.Object && item.TryGetProperty("release", out var release))
                    {
                        var releaseId = Id(release, "mbid") ?? Id(release, "caa_release_mbid");
                        var groupId = Id(release, "release_group_mbid");
                        if (releaseId is { } rid) cover = await FetchAsync("release", rid, ct);
                        if (cover is null && groupId is { } gid) cover = await FetchAsync("release-group", gid, ct);
                    }
                    cache.Entries.Set("recording:" + id, cover, new MemoryCacheEntryOptions
                    {
                        // Charge retained arrays here too: release entries may be evicted independently.
                        Size = (cover?.Bytes.Length ?? 0) + 128, AbsoluteExpirationRelativeToNow = cover is null ? lifetime : TimeSpan.FromDays(1)
                    });
                }
            }
        }
        finally { cache.Gate.Release(); }
    }

    private static Guid? Id(JsonElement element, string property) => element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String &&
        Guid.TryParseExact(value.GetString(), "D", out var id) && id != Guid.Empty ? id : null;

    private async Task<RecordingCover?> FetchAsync(string kind, Guid id, CancellationToken ct)
    {
        string key = kind + ":" + id;
        if (cache.Entries.TryGetValue(key, out RecordingCover? cached)) return cached;
        RecordingCover? cover = null;
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
                if (response.StatusCode == HttpStatusCode.NotFound) { lifetime = TimeSpan.FromHours(6); break; }
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
                cover = new(bytes, type);
                lifetime = TimeSpan.FromDays(1);
                break;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or OperationCanceledException) { }
        cache.Entries.Set(key, cover, new MemoryCacheEntryOptions
        {
            Size = (cover?.Bytes.Length ?? 0) + 128, AbsoluteExpirationRelativeToNow = lifetime
        });
        return cover;
    }

    private static bool Allowed(Uri uri) => uri.Scheme is "http" or "https" && uri.IsDefaultPort &&
        string.IsNullOrEmpty(uri.UserInfo) &&
        (uri.Host == "coverartarchive.org" || uri.Host == "archive.org" || uri.Host.EndsWith(".archive.org", StringComparison.Ordinal));

    private static bool ValidImage(byte[] bytes, string type) => type switch
    {
        "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff,
        "image/png" => bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        "image/webp" => bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
        _ => false
    };
}
