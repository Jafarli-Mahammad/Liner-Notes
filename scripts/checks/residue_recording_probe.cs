#:project ../../src/Infrastructure/Infrastructure.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Diagnostics;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;
using Microsoft.Extensions.Caching.Memory;

// Evaluation only: public examples, never production defaults or listener calibration.
const string usage = "Usage: residue_recording_probe.cs --live; at most 48 public requests, no credentials, no raw-response persistence";
if (args is ["--help"]) { Console.WriteLine(usage); return; }
if (args is not ["--live"]) throw new ArgumentException(usage);
var options = new ListenBrainzOptions { Mode = ListenBrainzClientMode.LiveOnly, LiveUseApproved = true, MaximumRequests = 40 };
options.Validate();
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
using var cache = new MemoryCache(new MemoryCacheOptions());
var gate = new MetaBrainzRequestGate(options, TimeProvider.System);
var client = new CountingClient(new ListenBrainzApiClient(http, options, gate, cache, TimeProvider.System));
using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(6));
var ct = deadline.Token;
var rows = new List<object>();
var optionalRequests = new List<object>();
var examples = new[]
{
    ("Teardrop", "Massive Attack"), ("So What", "Miles Davis"),
    ("Jolene", "Dolly Parton"), ("Roygbiv", "Boards of Canada"),
    ("La vie en rose", "Édith Piaf"), ("DNA.", "Kendrick Lamar"),
    ("Blinding Lights", "The Weeknd"), ("Clair de lune", "Isao Tomita")
};
foreach (var (title, artist) in examples)
{
    try
    {
        string query = Uri.EscapeDataString($"recording:\"{title}\" AND artist:\"{artist}\"");
        var search = await MusicBrainz("recording?fmt=json&limit=10&query=" + query, ct);
        var matches = search is { ValueKind: JsonValueKind.Object } root && root.TryGetProperty("recordings", out var found) && found.ValueKind == JsonValueKind.Array
            ? found.EnumerateArray().Where(r => r.ValueKind == JsonValueKind.Object && Text(r, "title").Equals(title, StringComparison.OrdinalIgnoreCase) &&
                r.TryGetProperty("artist-credit", out var credits) && credits.ValueKind == JsonValueKind.Array &&
                credits.EnumerateArray().Any(c => c.ValueKind == JsonValueKind.Object && c.TryGetProperty("artist", out var a) && Text(a, "name").Equals(artist, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(r => Text(r, "first-release-date") is { Length: > 0 } date ? date : "9999", StringComparer.Ordinal)
                .ThenBy(r => Text(r, "id"), StringComparer.Ordinal).ToArray() : [];
        if (matches.Length == 0)
        {
            rows.Add(new { title, artist, Missing = search is null ? "search_unavailable" : "no_exact_credit_in_bounded_search" });
            continue;
        }
        var id = Guid.Parse(Text(matches[0], "id"));
        var provider = new ListenBrainzRecommendationProvider(client);
        int before = client.Requests.Count;
        var watch = Stopwatch.StartNew();
        var raw = await provider.GetCandidatesByRecordingsAsync([id], 10, ct);
        var hydrated = await provider.HydrateCandidatesBatchAsync(raw.Items, ct);
        watch.Stop();
        rows.Add(new
        {
            title, artist, Recording = id, FirstReleaseDate = Text(matches[0], "first-release-date"),
            SearchMatches = matches.Length, Raw = raw.Count, Hydrated = hydrated.Count,
            RecordingTags = hydrated.Count(c => c.TagScope == "recording"),
            ArtistTags = hydrated.Count(c => c.TagScope == "artist"),
            RelationshipsWithoutTags = hydrated.Count(c => c.TagScope == "none"),
            HydrationLoss = raw.Count - hydrated.Count, Milliseconds = watch.Elapsed.TotalMilliseconds,
            Requests = client.Requests.Skip(before).ToArray(), Gaps = raw.Gaps.Concat(hydrated.Gaps).ToArray()
        });
        // Bounded optional route: one recording lookup, then one of its releases; no graph traversal.
        var detail = await MusicBrainz($"recording/{id:D}?fmt=json&inc=artist-rels+work-rels+work-level-rels+releases+release-groups", ct);
        if (detail is not { } data) continue;
        var relationships = data.TryGetProperty("relations", out var rels) && rels.ValueKind == JsonValueKind.Array ? rels.EnumerateArray().ToArray() : [];
        string? release = data.TryGetProperty("releases", out var releases) && releases.ValueKind == JsonValueKind.Array
            ? releases.EnumerateArray().Select(r => Text(r, "id")).FirstOrDefault(r => Guid.TryParse(r, out _)) : null;
        int albumRecordings = 0;
        bool containsSeed = false;
        if (release is not null && await MusicBrainz($"release/{release}?fmt=json&inc=recordings", ct) is { } album &&
            album.TryGetProperty("media", out var media) && media.ValueKind == JsonValueKind.Array)
        {
            var recordings = media.EnumerateArray().Where(m => m.ValueKind == JsonValueKind.Object && m.TryGetProperty("tracks", out var tracks) && tracks.ValueKind == JsonValueKind.Array)
                .SelectMany(m => m.GetProperty("tracks").EnumerateArray()).Where(t => t.ValueKind == JsonValueKind.Object && t.TryGetProperty("recording", out _))
                .Select(t => Text(t.GetProperty("recording"), "id")).Where(r => Guid.TryParse(r, out _)).Distinct().ToArray();
            albumRecordings = recordings.Length;
            containsSeed = recordings.Contains(id.ToString("D"));
        }
        rows.Add(new
        {
            Recording = id, Optional = "MusicBrainz", RecordingArtistRelationships = relationships.Count(r => Text(r, "target-type") == "artist"),
            WorkRelationships = relationships.Count(r => Text(r, "target-type") == "work"),
            AlbumRecordings = albumRecordings, ContainsSeed = containsSeed,
            Uncertainty = "Credits and release membership establish connections, not acoustic similarity or explicit favorites; one release only."
        });
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { rows.Add(new { title, artist, Missing = "probe_deadline" }); break; }
    catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or GenerationStoppedException or DiscoverySourceException)
    { rows.Add(new { title, artist, Missing = ex.GetType().Name }); }
}
Console.WriteLine(JsonSerializer.Serialize(new
{
    Version = "residue-recording-probe-v1", CapturedAtUtc = DateTimeOffset.UtcNow,
    Scope = "Eight independent public examples; bounded exact-credit search can miss recordings. No quality ratings or population coverage claim.",
    Requests = client.Requests, OptionalRequests = optionalRequests, Results = rows
}, new JsonSerializerOptions { WriteIndented = true }));

async Task<JsonElement?> MusicBrainz(string path, CancellationToken token)
{
    var watch = Stopwatch.StartNew();
    await gate.WaitAsync("MusicBrainz", token);
    using var message = new HttpRequestMessage(HttpMethod.Get, "https://musicbrainz.org/ws/2/" + path);
    message.Headers.UserAgent.ParseAdd(options.UserAgent);
    using var response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token);
    if ((int)response.StatusCode is 429 or 503)
    {
        var retry = response.Headers.RetryAfter?.Delta ??
            (response.Headers.RetryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : TimeSpan.FromSeconds(30));
        await gate.DeferAsync("MusicBrainz", retry < TimeSpan.Zero ? TimeSpan.Zero : retry, token);
    }
    using var input = await response.Content.ReadAsStreamAsync(token);
    using var bytes = new MemoryStream();
    var buffer = new byte[8192];
    int read;
    while ((read = await input.ReadAsync(buffer, token)) > 0)
    {
        if (bytes.Length + read > ListenBrainzResponse.MaximumBytes) throw new JsonException("probe_response_size_limit");
        bytes.Write(buffer, 0, read);
    }
    optionalRequests.Add(new { Path = path, Status = (int)response.StatusCode, Bytes = bytes.Length, Milliseconds = watch.Elapsed.TotalMilliseconds,
        RetrievedAtUtc = DateTimeOffset.UtcNow, Sha256 = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes.ToArray())) });
    if (!response.IsSuccessStatusCode) return null;
    using var document = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
    return document.RootElement.Clone();
}
static string Text(JsonElement value, string key) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var text) && text.ValueKind == JsonValueKind.String ? text.GetString() ?? "" : "";

sealed class CountingClient(IListenBrainzApiClient inner) : IListenBrainzApiClient
{
    public List<object> Requests { get; } = [];
    public async Task<ListenBrainzResponse> SendAsync(ListenBrainzRequest request, CancellationToken ct = default)
    {
        var watch = Stopwatch.StartNew();
        var response = await inner.SendAsync(request, ct);
        Requests.Add(new { request.Kind, request.Identity, Milliseconds = watch.Elapsed.TotalMilliseconds, response.Reference, response.Gaps, response.SharedCacheUntilUtc });
        return response;
    }
}
