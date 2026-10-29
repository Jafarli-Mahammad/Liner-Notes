using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Catalog;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Catalog;

namespace LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;

public sealed partial class ListenBrainzRecommendationProvider : IPublicRecordingCatalogSource
{
    public IReadOnlyList<CatalogLookupResult> ReadPublicRecordingMetadata()
        => ReadPublicRecordingMetadata(responses.Values.Where(value => value.Reference.Method == "metadata.recording"));

    internal static IReadOnlyList<CatalogLookupResult> ReadPublicRecordingMetadata(IEnumerable<ListenBrainzResponse> responses,
        IReadOnlySet<Guid>? allowedRecordings = null)
    {
        var results = new List<CatalogLookupResult>();
        foreach (var response in responses)
        {
            if (response.Reference.Origin != EvidenceOrigin.Live || response.Gaps.Length != 0 ||
                response.Reference.RetrievedAtUtc is not { } acquired ||
                response.SharedCacheUntilUtc is not { } expires || expires <= acquired ||
                response.Data is not { ValueKind: JsonValueKind.Object } data)
                continue;
            var artists = new Dictionary<Guid, CatalogArtifactRow>();
            var recordings = new List<CatalogArtifactRow>();
            foreach (var item in data.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                if (allowedRecordings is not null && (!Guid.TryParseExact(item.Name, "D", out var requested) || !allowedRecordings.Contains(requested))) continue;
                var recording = ListenBrainzEvidenceParser.Recording(response, item.Name);
                if (recording is null || !Text(recording.Title) ||
                    !item.Value.TryGetProperty("artist", out var credit) ||
                    !credit.TryGetProperty("artists", out var names) || names.ValueKind != JsonValueKind.Array)
                    continue;
                var ids = new List<Guid>();
                foreach (var artist in names.EnumerateArray())
                {
                    if (!artist.TryGetProperty("artist_mbid", out var id) || id.ValueKind != JsonValueKind.String ||
                        !Guid.TryParse(id.GetString(), out var canonical) || canonical == Guid.Empty ||
                        !artist.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String || !Text(name.GetString()))
                    { ids.Clear(); break; }
                    artists.TryAdd(canonical, new(canonical, CatalogKind.Artist, name.GetString()!, [], [], []));
                    ids.Add(canonical);
                }
                if (ids.Count is < 1 or > 32 || ids.Distinct().Count() != ids.Count) continue;
                // Tags and all user/taste/ranking fields are excluded from this CC0 projection.
                recordings.Add(new(Guid.Parse(recording.Mbid), CatalogKind.Track, recording.Title, [], ids.ToArray(), []));
            }
            if (recordings.Count == 0) continue;
            var rows = artists.Values.OrderBy(item => item.Id).Concat(recordings.OrderBy(item => item.Id)).ToArray();
            results.Add(new(rows, recordings.Select(item => item.Id).ToArray(), CatalogOrigin.Public,
                "MusicBrainz via ListenBrainz", "https://api.listenbrainz.org/1/metadata/recording/",
                acquired, expires, response.Reference.ResponseSha256!));
        }
        return results;
    }

    private static bool Text(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 256 || value.Any(char.IsControl)) return false;
        try
        {
            _ = CatalogText.Normalize(value);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
