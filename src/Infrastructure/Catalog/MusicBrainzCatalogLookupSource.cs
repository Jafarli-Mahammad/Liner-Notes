using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Catalog;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Catalog;
using LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;

namespace LinerNotes.Infrastructure.Catalog;

/// <summary>Explicit artist/release-group identity resolution; excludes supplementary tags from search responses.</summary>
public sealed class MusicBrainzCatalogLookupSource(IListenBrainzApiClient client, TimeProvider clock) : ICatalogLookupSource
{
    public async Task<CatalogLookupResult> SearchAsync(string query, CatalogKind kind, CancellationToken ct)
    {
        if (!Enum.IsDefined(kind) || string.IsNullOrWhiteSpace(query) || query.Length > 128 || query.Any(char.IsControl))
            throw new ArgumentException("catalog_lookup_query_invalid");
        var response = await client.SendAsync(new(kind switch { CatalogKind.Artist => ListenBrainzRequestKind.ArtistSearch, CatalogKind.Album => ListenBrainzRequestKind.AlbumSearch, _ => ListenBrainzRequestKind.RecordingSearch }, query.Trim(), 10), ct);
        if (!response.Gaps.IsEmpty)
            throw new LinerNotes.Application.Common.Interfaces.Discovery.DiscoverySourceException("catalog_source_unavailable",
                response.Gaps.Any(g => g.Reason is "rate_limit" or "transport_error" or "request_timeout" or "upstream_unavailable"));
        if (response.Data is not { } root || response.Reference.ResponseSha256 is not { } hash ||
            response.Reference.RetrievedAtUtc is not { } acquired)
            throw new GenerationStoppedException("catalog_lookup_unavailable");
        if (response.Reference.Origin is not (EvidenceOrigin.Live or EvidenceOrigin.Synthetic)) throw new GenerationStoppedException("catalog_lookup_recording_not_product_eligible");
        bool synthetic = response.Reference.Origin == EvidenceOrigin.Synthetic;
        var records = new List<CatalogArtifactRow>();
        var matched = new List<Guid>();
        string key = kind switch { CatalogKind.Artist => "artists", CatalogKind.Album => "release-groups", _ => "recordings" };
        if (!root.TryGetProperty(key, out var items) || items.ValueKind != JsonValueKind.Array) throw new GenerationStoppedException("catalog_lookup_schema_invalid");
        foreach (var item in items.EnumerateArray().Take(10))
        {
            var id = Identity(item);
            if (kind == CatalogKind.Artist) records.Add(Artist(item));
            else
            {
                if (!item.TryGetProperty("artist-credit", out var credit) || credit.ValueKind != JsonValueKind.Array || credit.GetArrayLength() is < 1 or > 32)
                    throw new GenerationStoppedException("catalog_lookup_artist_credit_missing");
                var ids = new List<Guid>();
                foreach (var part in credit.EnumerateArray())
                {
                    if (!part.TryGetProperty("artist", out var artist)) throw new GenerationStoppedException("catalog_lookup_artist_credit_missing");
                    records.Add(Artist(artist)); ids.Add(Identity(artist));
                }
                records.Add(new(id, kind, Text(item, "title", 256)!, [], ids.Distinct().ToArray(), [],
                    Text(item, "disambiguation", 500, false), Text(item, "first-release-date", 10, false), kind == CatalogKind.Track && item.TryGetProperty("releases", out var releases) && releases.ValueKind == JsonValueKind.Array
                        ? releases.EnumerateArray().Take(10).Select(r => new CatalogReleaseDto(Identity(r), Text(r, "title", 256)!, Text(r, "date", 10, false))).ToArray() : null));
            }
            matched.Add(id);
        }
        return new(records.DistinctBy(r => (r.Kind, r.Id)).OrderBy(r => r.Kind).ToArray(), matched.Distinct().ToArray(),
            synthetic ? CatalogOrigin.Synthetic : CatalogOrigin.Public, synthetic ? "generated-fixture" : "MusicBrainz",
            "https://musicbrainz.org/ws/2/" + (kind switch { CatalogKind.Artist => "artist", CatalogKind.Album => "release-group", _ => "recording" }), acquired,
            synthetic ? clock.GetUtcNow().AddHours(1) : response.SharedCacheUntilUtc, hash);
    }

    private static CatalogArtifactRow Artist(JsonElement artist)
    {
        var aliases = artist.TryGetProperty("aliases", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Take(12).Select(a => Text(a, "name", 256)!).Distinct(StringComparer.Ordinal).ToArray() : [];
        return new(Identity(artist), CatalogKind.Artist, Text(artist, "name", 256)!, aliases, [], [], Text(artist, "disambiguation", 500, false));
    }
    private static Guid Identity(JsonElement value) => value.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String && Guid.TryParse(id.GetString(), out var result) && result != Guid.Empty
        ? result : throw new GenerationStoppedException("catalog_lookup_identity_invalid");
    private static string? Text(JsonElement value, string key, int maximum, bool required = true)
    {
        if (!value.TryGetProperty(key, out var item) || item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            return required ? throw new GenerationStoppedException("catalog_lookup_schema_invalid") : null;
        string text = item.GetString()!.Trim();
        if (text.Length > maximum || text.Any(char.IsControl)) throw new GenerationStoppedException("catalog_lookup_schema_invalid");
        return text;
    }
}
