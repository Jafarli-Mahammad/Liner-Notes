using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;

namespace LinerNotes.Infrastructure.Artwork;

// Canonical MusicBrainz relationships -> Wikidata P18 -> licensed Commons thumbnail.
// No name matching, scraping, arbitrary hosts, redirects, or user-provided image URLs.
public sealed class MusicBrainzArtistArtwork(IListenBrainzApiClient metadata, IHttpClientFactory clients,
    IMetaBrainzRequestGate gate, ListenBrainzOptions options, TimeProvider clock) : IArtistArtworkAcquirer
{
    private DateTimeOffset metadataExpiry = DateTimeOffset.MaxValue;

    public async Task<ArtistPhoto?> AcquireAsync(Guid artistId, CancellationToken ct)
    {
        metadataExpiry = DateTimeOffset.MaxValue;
        if (options.Mode != ListenBrainzClientMode.LiveOnly || !options.LiveUseApproved) return null;
        var response = await metadata.SendAsync(new(ListenBrainzRequestKind.ArtistArtworkMetadata, artistId.ToString("D")), ct);
        if (response.Gaps.Any(g => g.Reason is "rate_limit" or "transport_error" or "request_timeout" or "upstream_unavailable"))
            throw new HttpRequestException("artist_metadata_temporarily_unavailable");
        if (response.SharedCacheUntilUtc is not { } expiry || response.Data is not { } artist ||
            !artist.TryGetProperty("relations", out var relations)) return null;
        string? file = null, entity = null;
        foreach (var relation in relations.EnumerateArray())
        {
            if (!relation.TryGetProperty("url", out var url) || !url.TryGetProperty("resource", out var resource) ||
                !Uri.TryCreate(resource.GetString(), UriKind.Absolute, out var uri) || uri.Scheme != "https" || !uri.IsDefaultPort) continue;
            if (uri.Host == "commons.wikimedia.org" && uri.AbsolutePath.StartsWith("/wiki/File:", StringComparison.Ordinal))
                file = Uri.UnescapeDataString(uri.AbsolutePath[11..]);
            if (uri.Host == "www.wikidata.org" && Regex.IsMatch(uri.AbsolutePath, "^/wiki/Q[1-9][0-9]{0,10}$"))
                entity = uri.AbsolutePath[6..];
        }
        if (file is null && entity is not null)
        {
            using var data = await ReadJsonAsync($"https://www.wikidata.org/w/api.php?action=wbgetentities&ids={entity}&props=claims&format=json&maxage=3600&smaxage=3600", ct);
            if (data is null || !data.RootElement.GetProperty("entities").TryGetProperty(entity, out var value) ||
                !value.GetProperty("claims").TryGetProperty("P18", out var images)) return null;
            foreach (var image in images.EnumerateArray())
            {
                if (image.TryGetProperty("rank", out var rank) && rank.GetString() == "deprecated") continue;
                if (image.GetProperty("mainsnak").TryGetProperty("datavalue", out var imageValue))
                { file = imageValue.GetProperty("value").GetString(); break; }
            }
        }
        if (string.IsNullOrWhiteSpace(file) || file.Length > 300 || file.Any(char.IsControl)) return null;
        using var info = await ReadJsonAsync("https://commons.wikimedia.org/w/api.php?action=query&format=json&maxage=3600&smaxage=3600&prop=imageinfo&iiprop=url%7Cextmetadata&iiurlwidth=160&titles=" + Uri.EscapeDataString("File:" + file), ct);
        if (info is null) return null;
        foreach (var page in info.RootElement.GetProperty("query").GetProperty("pages").EnumerateObject())
        {
            if (!page.Value.TryGetProperty("imageinfo", out var imageList) || imageList.GetArrayLength() == 0) continue;
            var image = imageList[0];
            if (!image.TryGetProperty("thumburl", out var thumb) || !image.TryGetProperty("extmetadata", out var rights)) continue;
            var credit = Plain(Value(rights, "Artist"));
            var license = Plain(Value(rights, "LicenseShortName"));
            var licenseUrl = Value(rights, "LicenseUrl");
            var source = image.GetProperty("descriptionurl").GetString();
            if (!AllowedLicense(license, licenseUrl) || string.IsNullOrWhiteSpace(credit) || credit.Length > 1000 ||
                !AllowedUrl(source, "commons.wikimedia.org") || !(AllowedUrl(thumb.GetString(), "upload.wikimedia.org") || AllowedUrl(thumb.GetString(), "thumb.wikimedia.org"))) continue;
            using var result = await SendAsync(thumb.GetString()!, ct);
            if (!result.IsSuccessStatusCode || result.Headers.CacheControl is { NoStore: true } or { NoCache: true } or { Private: true }) return null;
            var type = result.Content.Headers.ContentType?.MediaType;
            var bytes = await ReadBoundedAsync(result, ct);
            if (!(type == "image/jpeg" && bytes.AsSpan().StartsWith(new byte[] { 0xff, 0xd8, 0xff })) &&
                !(type == "image/png" && bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))) return null;
            var retainUntil = new[] { expiry, metadataExpiry, clock.GetUtcNow().AddHours(1), CacheExpiry(result) }.Min();
            if (retainUntil <= clock.GetUtcNow()) return null;
            return new(bytes, type!, source!, credit, license, licenseUrl, retainUntil);
        }
        return null;
    }

    public static bool AllowedLicense(string license, string url) =>
        license is "Public domain" or "CC0" ||
        (Regex.IsMatch(license, "^CC BY(-SA)? [1-4]\\.[0-9]$") && Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
         uri.Scheme == "https" && uri.Host == "creativecommons.org" &&
         Regex.IsMatch(uri.AbsolutePath, "^/licenses/by(-sa)?/[1-4]\\.[0-9]/?$"));
    private static bool AllowedUrl(string? value, string host) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == "https" && uri.Host == host && uri.IsDefaultPort && uri.UserInfo.Length == 0;
    private static string Value(JsonElement metadata, string key) => metadata.TryGetProperty(key, out var item) &&
        item.TryGetProperty("value", out var value) ? value.GetString() ?? "" : "";
    private static string Plain(string html) => WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]*>", ""));
    private static bool Cacheable(HttpResponseMessage response) => response.Headers.CacheControl is { NoStore: false, NoCache: false, Private: false } cache &&
        (cache.SharedMaxAge ?? cache.MaxAge) is { } age && age > TimeSpan.Zero;
    private DateTimeOffset CacheExpiry(HttpResponseMessage response) => clock.GetUtcNow() +
        ArtworkFreshness.Remaining(response, clock.GetUtcNow(), TimeSpan.FromHours(1));
    private async Task<HttpResponseMessage> SendAsync(string url, CancellationToken ct)
    {
        await gate.WaitAsync("Wikimedia", ct);
        var client = clients.CreateClient("ArtistArtwork");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(options.UserAgent);
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if ((int)response.StatusCode is 429 or 503)
        {
            var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(30);
            await gate.DeferAsync("Wikimedia", delay > TimeSpan.FromDays(1) ? TimeSpan.FromDays(1) : delay, ct);
            response.Dispose(); throw new HttpRequestException("artist_artwork_temporarily_unavailable");
        }
        return response;
    }
    private async Task<JsonDocument?> ReadJsonAsync(string url, CancellationToken ct)
    {
        using var response = await SendAsync(url, ct);
        if (!response.IsSuccessStatusCode || !Cacheable(response)) return null;
        metadataExpiry = new[] { metadataExpiry, CacheExpiry(response) }.Min();
        if (metadataExpiry <= clock.GetUtcNow()) return null;
        return JsonDocument.Parse(await ReadBoundedAsync(response, ct));
    }
    private static async Task<byte[]> ReadBoundedAsync(HttpResponseMessage response, CancellationToken ct)
    {
        const int maximum = 1024 * 1024;
        if (response.Content.Headers.ContentLength > maximum) throw new HttpRequestException("artist_artwork_size_limit");
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await input.ReadAsync(buffer, ct)) > 0)
        {
            if (output.Length + read > maximum) throw new HttpRequestException("artist_artwork_size_limit");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }
}
