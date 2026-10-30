using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Catalog;

namespace LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;

public sealed partial class ListenBrainzRecommendationProvider
{
    public async Task<SeedTagSnapshot> GetArtistFromRecordingAsync(Guid recordingId, string artistName, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(new(ListenBrainzRequestKind.RecordingMetadata, recordingId.ToString("D")), cancellationToken);
        LinerNotes.Application.Features.Discovery.DiscoverySourceValidation.ThrowIfFailed(response.Gaps);
        var credit = ListenBrainzEvidenceParser.Recording(response, recordingId.ToString("D"));
        if (credit is null || credit.ArtistMbids.Count != 1 || !string.Equals(credit.ArtistName.Trim(), artistName.Trim(), StringComparison.OrdinalIgnoreCase))
            return await GetAsync(artistName, cancellationToken);
        var seed = await GetArtistAsync(Guid.Parse(credit.ArtistMbids[0]), artistName, cancellationToken);
        return seed with { SupportingResponses = seed.SupportingResponses.Append(response.Reference).Distinct().ToArray() };
    }

    public Task<IngestionResult<RawCandidateTrack>> GetCandidatesByRecordingsAsync(IReadOnlyList<Guid> ids, int limit, CancellationToken ct)
        => DiscoverAsync(ids.Select(id => id.ToString("D")).ToArray(), limit, false, ct, true, true);

    public async Task<SeedTagSnapshot> GetRecordingAsync(Guid id, string name, CancellationToken ct)
    {
        var response = await SendAsync(new(ListenBrainzRequestKind.RecordingMetadata, id.ToString("D")), ct);
        return RecordingTags(response, id, name);
    }

    // Only recording-scoped observations qualify; artist tags never stand in for a recording.
    private static SeedTagSnapshot RecordingTags(ListenBrainzResponse response, Guid id, string name)
    {
        var observations = new List<TagObservation>();
        if (response.Data is { ValueKind: JsonValueKind.Object } root && root.TryGetProperty(id.ToString("D"), out var recording) &&
            recording.TryGetProperty("tag", out var scope) && scope.TryGetProperty("recording", out var tags) && tags.ValueKind == JsonValueKind.Array)
        {
            int position = 0;
            foreach (var tag in tags.EnumerateArray().Take(100))
            {
                position++;
                if (!tag.TryGetProperty("tag", out var label) || label.ValueKind != JsonValueKind.String ||
                    label.GetString() is not { Length: > 0 and <= 100 } text || text.Any(char.IsControl) ||
                    !tag.TryGetProperty("count", out var count) || !count.TryGetInt64(out var votes) || votes <= 0) continue;
                observations.Add(new(text.Trim().ToLowerInvariant(), new ObservedValue<long>(votes, votes.ToString(System.Globalization.CultureInfo.InvariantCulture), null),
                    null, response.Reference, null, position)
                { EntityIdentity = "recording:" + id.ToString("D") });
            }
        }
        double max = observations.Count == 0 ? 1 : observations.Max(t => t.Count.Value!.Value);
        var vector = WeightedTagVector.FromDictionary(observations.GroupBy(t => t.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Max(t => t.Count.Value!.Value) / max, StringComparer.Ordinal));
        return new(name, vector, observations, response.Reference,
            observations.Count == 0 ? "direct_recording_tags_missing" : null, response.Gaps);
    }
}
