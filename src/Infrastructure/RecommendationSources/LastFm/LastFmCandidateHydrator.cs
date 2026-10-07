using System.Globalization;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Catalog;
using LinerNotes.Infrastructure.RecommendationSources.LastFm.Models;
using Microsoft.Extensions.Logging;

namespace LinerNotes.Infrastructure.RecommendationSources.LastFm;

/// <summary>Supplied artist tags only; absent evidence is retained without a generic tag or popularity estimate.</summary>
public sealed class LastFmCandidateHydrator(ILastFmApiClient apiClient, ILogger<LastFmCandidateHydrator> logger) : ICandidateHydrator
{
    private static readonly HashSet<string> StoplistTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "seen live", "favorites", "favourite", "favorite", "loved", "spotify",
        "albums i own", "my favorites", "fip", "under 2000 listeners", "beautiful", "cool"
    };

    public async Task<HydratedCandidateTrack?> HydrateCandidateAsync(RawCandidateTrack rawCandidate, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (rawCandidate is null) return null;
        var batch = await HydrateCandidatesBatchAsync([rawCandidate], cancellationToken).ConfigureAwait(false);
        return batch.Count > 0 ? batch[0] : null;
    }

    public async Task<IngestionResult<HydratedCandidateTrack>> HydrateCandidatesBatchAsync(IReadOnlyList<RawCandidateTrack> rawCandidates,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (rawCandidates is null || rawCandidates.Count == 0) return new([]);
        if (rawCandidates.Select(c => c.Origin).Distinct().Count() != 1) throw new InvalidOperationException("Mixed origins in hydration input.");
        var result = new List<HydratedCandidateTrack>();
        var gaps = new List<CoverageGap>();
        var responses = new Dictionary<string, LastFmResponse<LastFmTagItem>>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in rawCandidates.OrderBy(c => c.CandidateKey, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!responses.TryGetValue(raw.ArtistName, out var response))
            {
                response = await apiClient.GetArtistTopTagsAsync(raw.ArtistName, 15, cancellationToken).ConfigureAwait(false);
                responses[raw.ArtistName] = response;
                gaps.AddRange(response.Gaps);
            }
            if (raw.Origin != response.Reference.Origin) throw new InvalidOperationException("Tag hydration cannot blend origins.");
            if (response.Reference.Provider != "Last.fm" || response.Reference.Method != "artist.gettoptags" ||
                !StringComparer.OrdinalIgnoreCase.Equals(response.Reference.RequestIdentity, raw.ArtistName))
                throw new InvalidOperationException("Tag response provenance does not match candidate artist.");
            var observations = response.Select((tag, index) =>
            {
                string? countRaw = tag.RawCount ?? tag.Count?.ToString(CultureInfo.InvariantCulture);
                var count = LastFmEvidenceParser.Count(countRaw);
                string? reason = string.IsNullOrWhiteSpace(tag.Name) ? "missing_name" :
                    StoplistTags.Contains(tag.Name.Trim()) ? "stoplist" : !count.IsValid ? count.MissingReason :
                    count.Value == 0 ? "zero_count" : null;
                return new TagObservation(tag.Name, count, tag.Url, response.Reference, reason, tag.ResponsePosition ?? index + 1);
            }).OrderBy(t => t.Name.Trim().ToLowerInvariant(), StringComparer.Ordinal)
                .ThenBy(t => t.Count.RawValue, StringComparer.Ordinal).ToArray();
            var valid = observations.Where(t => t.ExclusionReason is null).ToArray();
            double maximum = valid.Length == 0 ? 0 : valid.Max(t => t.Count.Value!.Value);
            var weights = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var tag in valid)
            {
                string key = tag.Name.Trim().ToLowerInvariant();
                double value = tag.Count.Value!.Value / maximum;
                weights[key] = Math.Max(weights.GetValueOrDefault(key), value);
            }
            string? missing = valid.Length == 0 ? "no_valid_descriptive_tag_counts" : null;
            var candidateGaps = response.Gaps.ToList();
            if (missing is not null)
            {
                var gap = new CoverageGap(raw.ArtistName, response.Reference.Method, missing, response.Reference);
                gaps.Add(gap); candidateGaps.Add(gap);
            }
            result.Add(new(Track.Create(raw.Title, raw.ArtistName, mbid: raw.Mbid), WeightedTagVector.FromDictionary(weights),
                raw, observations, response.Reference, missing, candidateGaps));
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (gaps.Count > 0) logger.LogInformation("Candidate hydration contains explicit evidence gaps.");
        return new(result, gaps);
    }
}
