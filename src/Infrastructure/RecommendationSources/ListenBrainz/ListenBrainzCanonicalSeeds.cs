using System.Globalization;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Catalog;
namespace LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;
public sealed partial class ListenBrainzRecommendationProvider
{
    private long workingBytes;
    private bool sharedCachingProhibited;
    private DateTimeOffset? sharedUntil;
    public DateTimeOffset? SharedUntilUtc { get => sharedCachingProhibited ? null : sharedUntil; private set => sharedUntil = value; }
    public Task<IngestionResult<RawCandidateTrack>> GetCandidatesByCanonicalArtistsAsync(IReadOnlyList<Guid> ids, int limit, CancellationToken ct)
        => DiscoverAsync(ids.Select(id => id.ToString("D")).ToArray(), limit, false, ct, canonical: true);
    public async Task<SeedTagSnapshot> GetArtistAsync(Guid artistId, string name, CancellationToken ct)
    {
        var response = await SendAsync(new(ListenBrainzRequestKind.ArtistMetadata, artistId.ToString("D")), ct);
        var tags = ListenBrainzEvidenceParser.ArtistTags([(artistId.ToString("D"), response)]);
        return new(name, tags.Vector, tags.Tags, response.Reference, tags.MissingReason, tags.Gaps);
    }
    public async Task<SeedTagSnapshot> GetAlbumAsync(Guid releaseGroupId, string name, CancellationToken ct)
    {
        var response = await SendAsync(new(ListenBrainzRequestKind.AlbumMetadata, releaseGroupId.ToString("D")), ct);
        return ParseAlbumTags(response, releaseGroupId, name);
    }
    public static SeedTagSnapshot ParseAlbumTags(ListenBrainzResponse response, Guid id, string name)
    {
        var observations = new List<TagObservation>();
        if (response.Data is { ValueKind: JsonValueKind.Object } root && root.TryGetProperty(id.ToString("D"), out var album) &&
            album.TryGetProperty("tag", out var tag) && tag.ValueKind == JsonValueKind.Object &&
            tag.TryGetProperty("release_group", out var tags) && tags.ValueKind == JsonValueKind.Array)
        {
            int position = 0;
            foreach (var item in tags.EnumerateArray().Take(100))
            {
                position++;
                if (item.ValueKind != JsonValueKind.Object) continue;
                string label = item.TryGetProperty("tag", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
                string? raw = item.TryGetProperty("count", out var count) ? count.ToString() : null;
                var votes = long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long n) && n > 0
                    ? new ObservedValue<long>(n, raw, null) : ObservedValue<long>.Missing("invalid_tag_vote_count", raw);
                string? exclusion = string.IsNullOrWhiteSpace(label) || label.Length > 100 ? "invalid_tag_name" : votes.MissingReason;
                observations.Add(new(label, votes, null, response.Reference, exclusion, position) { EntityIdentity = "release-group:" + id.ToString("D") });
            }
        }
        var valid = observations.Where(t => t.ExclusionReason is null).ToArray();
        double max = valid.Length == 0 ? 1 : valid.Max(t => t.Count.Value!.Value);
        var vector = WeightedTagVector.FromDictionary(valid.GroupBy(t => t.Name.Trim().ToLowerInvariant(), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Max(t => t.Count.Value!.Value) / max, StringComparer.Ordinal));
        return new(name, vector, observations, response.Reference, valid.Length == 0 ? "no_valid_release_group_tags" : null, response.Gaps);
    }
}
