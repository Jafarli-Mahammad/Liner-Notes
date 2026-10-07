using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Infrastructure.RecommendationSources.LastFm.Models;

namespace LinerNotes.Infrastructure.RecommendationSources.LastFm;

public static class LastFmEvidenceParser
{
    public static ObservedValue<double> Match(string? raw)
    {
        if (raw is null) return ObservedValue<double>.Missing();
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return ObservedValue<double>.Missing("malformed", raw);
        if (!double.IsFinite(value)) return ObservedValue<double>.Missing("nonfinite", raw);
        if (value is < 0 or > 1) return ObservedValue<double>.Missing("out_of_range", raw);
        return new(value, raw, null);
    }

    public static ObservedValue<long> Count(string? raw)
    {
        if (raw is null) return ObservedValue<long>.Missing();
        if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            return ObservedValue<long>.Missing("malformed_or_overflow", raw);
        if (value < 0) return ObservedValue<long>.Missing("out_of_range", raw);
        return new(value, raw, null);
    }

    public static LastFmResponse<T> Parse<T>(string method, string identity, ReadOnlySpan<byte> body,
        EvidenceOrigin origin, DateTimeOffset timestamp, int limit)
    {
        var reference = new ResponseReference("Last.fm", method, identity, origin,
            Convert.ToHexStringLower(SHA256.HashData(body)), timestamp.ToUniversalTime());
        var gaps = new List<CoverageGap>();
        var items = new List<T>();
        try
        {
            using var doc = JsonDocument.Parse(body.ToArray());
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return Gap<T>(reference, "invalid_response_shape");
            if (root.TryGetProperty("error", out var error))
                return Gap<T>(reference, Scalar(error) == "29" ? "rate_limit" : "provider_error");
            (string container, string item) = method switch
            {
                "artist.getsimilar" => ("similarartists", "artist"),
                "artist.gettoptags" => ("toptags", "tag"),
                "artist.gettoptracks" => ("toptracks", "track"),
                "tag.gettoptracks" => ("tracks", "track"),
                _ => throw new ArgumentException("Unsupported endpoint.", nameof(method))
            };
            if (!root.TryGetProperty(container, out var collection) || collection.ValueKind != JsonValueKind.Object ||
                !collection.TryGetProperty(item, out var entries)) return Gap<T>(reference, "missing_collection");
            if (entries.ValueKind is not (JsonValueKind.Array or JsonValueKind.Object or JsonValueKind.Null))
                return Gap<T>(reference, "invalid_collection");
            IEnumerable<JsonElement> elements = entries.ValueKind switch
            {
                JsonValueKind.Array => entries.EnumerateArray().ToArray(),
                JsonValueKind.Object => [entries],
                JsonValueKind.Null => [],
                _ => []
            };
            int position = 0;
            foreach (var element in elements.Take(limit))
            {
                position++;
                string? name = Text(element, "name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    gaps.Add(new(identity, method, "invalid_item_identity", reference));
                    continue;
                }
                object parsed;
                if (method == "artist.getsimilar")
                    parsed = new LastFmArtistSummary(name, Text(element, "mbid"), Raw(element, "match"), Text(element, "url"), position);
                else if (method == "artist.gettoptags")
                {
                    string? raw = Raw(element, "count");
                    parsed = new LastFmTagItem(name, Count(raw).Value, Text(element, "url"), raw, position);
                }
                else
                {
                    LastFmArtistRef? artist = null;
                    if (element.TryGetProperty("artist", out var artistElement))
                    {
                        if (artistElement.ValueKind == JsonValueKind.Object)
                            artist = new(Text(artistElement, "name") ?? "", Text(artistElement, "mbid"), Text(artistElement, "url"));
                        else if (artistElement.ValueKind == JsonValueKind.String)
                            artist = new(artistElement.GetString() ?? "", null, null);
                    }
                    // artist.getTopTracks is scoped to its requested artist, even when the
                    // response omits a repeated artist field. tag.getTopTracks has no such scope.
                    else if (method == "artist.gettoptracks") artist = new(identity, null, null);
                    string? rank = null;
                    if (element.TryGetProperty("@attr", out var attrs)) rank = Raw(attrs, "rank");
                    rank ??= Raw(element, "rank");
                    parsed = new LastFmTrackItem(name, Text(element, "mbid"), Text(element, "url"), Raw(element, "duration"),
                        Raw(element, "listeners"), Raw(element, "playcount"), artist, rank, position);
                }
                items.Add((T)parsed);
            }
            if (items.Count == 0) gaps.Add(new(identity, method, "empty_response", reference));
            return new(items, reference, gaps);
        }
        catch (JsonException) { return Gap<T>(reference, "malformed_json"); }
    }

    public static LastFmResponse<T> Gap<T>(ResponseReference reference, string reason) =>
        new([], reference, [new(reference.RequestIdentity, reference.Method, reason, reference)]);

    private static string? Text(JsonElement element, string key) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? (string.IsNullOrWhiteSpace(value.GetString()) ? null : value.GetString()) : null;
    private static string? Raw(JsonElement element, string key) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out var value) ? Scalar(value) : null;
    private static string? Scalar(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String => value.GetString(),
        _ => value.GetRawText()
    };
}
