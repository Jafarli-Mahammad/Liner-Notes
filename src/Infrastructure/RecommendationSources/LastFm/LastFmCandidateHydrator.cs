using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Catalog;
using LinerNotes.Infrastructure.RecommendationSources.LastFm.Models;
using Microsoft.Extensions.Logging;

namespace LinerNotes.Infrastructure.RecommendationSources.LastFm;

/// <summary>Supplied artist tags only; absent evidence is retained without a generic tag or popularity estimate.</summary>
public sealed class LastFmCandidateHydrator(ILastFmApiClient apiClient, ILogger<LastFmCandidateHydrator> logger) : ICandidateHydrator
{
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
            var tags = LastFmTagEvidence.Materialize(response);
            gaps.AddRange(tags.Gaps.Where(g => !response.Gaps.Contains(g)));
            result.Add(new(Track.Create(raw.Title, raw.ArtistName, mbid: raw.Mbid), tags.Vector,
                raw, tags.Observations, response.Reference, tags.MissingReason, tags.Gaps));
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (gaps.Count > 0) logger.LogInformation("Candidate hydration contains explicit evidence gaps.");
        return new(result, gaps);
    }
}
