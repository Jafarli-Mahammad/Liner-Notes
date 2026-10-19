using System.Globalization;
using System.Text.Json;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Catalog;

namespace LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;

public sealed record ResolvedArtist(string Mbid, string Name);
public sealed record RecordingMetadata(string Mbid, string Title, string ArtistName, IReadOnlyList<string> ArtistMbids);
public sealed record ListenBrainzTagSnapshot(WeightedTagVector Vector, IReadOnlyList<TagObservation> Tags,
    string? MissingReason, IReadOnlyList<CoverageGap> Gaps);

/// <summary>Shared HTTP/replay interpretation. No cross-provider count or similarity substitution.</summary>
public static class ListenBrainzEvidenceParser
{
    public const string NormalizationVersion = "max-observed-artist-tag-votes-v1";
    private static readonly HashSet<string> Stoplist = new(StringComparer.OrdinalIgnoreCase)
        { "albums i own", "beautiful", "cool", "favorite", "favorites", "favourite", "fip", "loved", "my favorites", "seen live", "spotify", "under 2000 listeners" };

    internal static string? Text(JsonElement element, string key) => element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    internal static JsonElement? Property(JsonElement element, string key) => element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(key, out var value) ? value : null;
    internal static bool Mbid(string? value) => Guid.TryParseExact(value, "D", out var id) && id != Guid.Empty;

    public static ResolvedArtist? ResolveArtist(ListenBrainzResponse response, string name, out string? reason)
    {
        reason = "artist_not_resolved";
        if (response.Data is not { } data || Property(data, "artists") is not { ValueKind: JsonValueKind.Array } artists) return null;
        // Never treat a search engine score as identity proof. Exact normalized names or supplied aliases only.
        var matches = artists.EnumerateArray().Where(a =>
            string.Equals(Text(a, "name")?.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase) ||
            (Property(a, "aliases") is { ValueKind: JsonValueKind.Array } aliases && aliases.EnumerateArray().Any(alias =>
                string.Equals(Text(alias, "name")?.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase))))
            .Where(a => Mbid(Text(a, "id"))).GroupBy(a => Text(a, "id")!, StringComparer.OrdinalIgnoreCase).ToArray();
        if (matches.Length != 1) { reason = matches.Length > 1 ? "artist_ambiguous" : "artist_not_resolved"; return null; }
        // A truncated result set can hide another exact namesake. Require a complete bounded search response.
        if (Property(data, "count") is not { ValueKind: JsonValueKind.Number } total || !total.TryGetInt32(out int count) || count < 0 || count > artists.GetArrayLength())
        { reason = "artist_search_incomplete"; return null; }
        reason = null;
        return new(matches[0].Key.ToLowerInvariant(), Text(matches[0].First(), "name") ?? name);
    }

    public static IReadOnlyList<string> RecordingIds(ListenBrainzResponse response, int limit, out bool invalidShape)
    {
        invalidShape = false;
        if (response.Data is not { } data) return [];
        var values = new List<JsonElement>();
        if (response.Reference.Method == "lb-radio.artist" && data.ValueKind == JsonValueKind.Object)
        {
            foreach (var group in data.EnumerateObject())
            {
                if (group.Value.ValueKind != JsonValueKind.Array) { invalidShape = true; continue; }
                values.AddRange(group.Value.EnumerateArray());
            }
        }
        else if (response.Reference.Method is "lb-radio.tags" or "similar-recordings" && data.ValueKind == JsonValueKind.Array) values.AddRange(data.EnumerateArray());
        else { invalidShape = true; return []; }
        values.RemoveAll(v => v.ValueKind == JsonValueKind.Object && v.TryGetProperty("comment", out _));
        if (values.Any(v => !Mbid(Text(v, "recording_mbid")))) invalidShape = true;
        return values.Select(v => Text(v, "recording_mbid")).Where(Mbid).Select(id => id!.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal).Take(limit).ToArray();
    }

    public static RecordingMetadata? Recording(ListenBrainzResponse response, string mbid)
    {
        if (response.Data is not { } data || Property(data, mbid) is not { ValueKind: JsonValueKind.Object } item ||
            Property(item, "recording") is not { } recording || Property(item, "artist") is not { } artist) return null;
        string? title = Text(recording, "name"), name = Text(artist, "name");
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(name) ||
            Property(artist, "artists") is not { ValueKind: JsonValueKind.Array } artists) return null;
        var ids = artists.EnumerateArray().Select(a => Text(a, "artist_mbid")).ToArray();
        if (ids.Length == 0 || ids.Any(id => !Mbid(id))) return null;
        return new(mbid, title, name, ids.Select(id => id!.ToLowerInvariant()).Distinct(StringComparer.Ordinal).ToArray());
    }

    public static ListenBrainzTagSnapshot ArtistTags(IEnumerable<(string Mbid, ListenBrainzResponse Response)> inputs)
    {
        var observations = new List<TagObservation>();
        var gaps = new List<CoverageGap>();
        foreach (var (mbid, response) in inputs)
        {
            gaps.AddRange(response.Gaps);
            JsonElement? found = null;
            if (response.Data is { ValueKind: JsonValueKind.Array } data)
                foreach (var item in data.EnumerateArray())
                    if (string.Equals(Text(item, "artist_mbid"), mbid, StringComparison.OrdinalIgnoreCase))
                    { found = item; break; }
            if (found is not { } entity || Property(entity, "tag") is not { } tag ||
                Property(tag, "artist") is not { ValueKind: JsonValueKind.Array } tags)
            { gaps.Add(new(mbid, response.Reference.Method, "artist_tags_missing", response.Reference)); continue; }
            int position = 0;
            foreach (var item in tags.EnumerateArray())
            {
                position++;
                string name = Text(item, "tag") ?? "";
                string? entityId = Text(item, "artist_mbid");
                var countValue = Property(item, "count");
                string? raw = countValue?.ToString();
                var count = long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long number) && number >= 0
                    ? new ObservedValue<long>(number, raw, null) : ObservedValue<long>.Missing("invalid_tag_vote_count", raw);
                // The enclosing metadata entity is already matched to this MBID. Older/newer
                // responses may omit a redundant inner ID; a conflicting supplied ID is rejected.
                string? exclusion = entityId is not null && !string.Equals(entityId, mbid, StringComparison.OrdinalIgnoreCase) ? "tag_artist_mismatch" :
                    string.IsNullOrWhiteSpace(name) ? "missing_name" : Stoplist.Contains(name.Trim()) ? "stoplist" :
                    !count.IsValid ? count.MissingReason : count.Value == 0 ? "zero_count" : null;
                observations.Add(new(name, count, null, response.Reference, exclusion, position) { EntityIdentity = entityId ?? mbid });
            }
        }
        var valid = observations.Where(t => t.ExclusionReason is null).ToArray();
        // Max per normalized tag across credited artists, then max normalization. No synthetic votes.
        double maximum = valid.Length == 0 ? 0 : valid.Max(t => t.Count.Value!.Value);
        var weights = valid.GroupBy(t => t.Name.Trim().ToLowerInvariant(), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Max(t => t.Count.Value!.Value) / maximum, StringComparer.Ordinal);
        return new(WeightedTagVector.FromDictionary(weights), observations, valid.Length == 0 ? "no_valid_artist_tag_votes" : null, gaps);
    }
}
