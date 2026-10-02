using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Scoring;
using Microsoft.Extensions.Logging;

namespace LinerNotes.Infrastructure.RecommendationSources.LastFm;

/// <summary>
/// Enriches raw candidate tracks using Last.fm artist.getTopTags to synthesize
/// normalized WeightedTagVector and GlobalPopularity attributes for Residue scoring.
/// </summary>
public sealed class LastFmCandidateHydrator : ICandidateHydrator
{
    private static readonly HashSet<string> StoplistTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "seen live", "favorites", "favourite", "favorite", "loved", "spotify",
        "albums i own", "my favorites", "fip", "under 2000 listeners", "beautiful", "cool"
    };

    private readonly ILastFmApiClient _apiClient;
    private readonly ILogger<LastFmCandidateHydrator> _logger;

    public LastFmCandidateHydrator(
        ILastFmApiClient apiClient,
        ILogger<LastFmCandidateHydrator> logger)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CandidateTrack?> HydrateCandidateAsync(
        RawCandidateTrack rawCandidate,
        CancellationToken cancellationToken = default)
    {
        if (rawCandidate is null)
            return null;

        var batch = await HydrateCandidatesBatchAsync(new[] { rawCandidate }, cancellationToken).ConfigureAwait(false);
        return batch.Count > 0 ? batch[0] : null;
    }

    public async Task<IReadOnlyList<CandidateTrack>> HydrateCandidatesBatchAsync(
        IReadOnlyList<RawCandidateTrack> rawCandidates,
        CancellationToken cancellationToken = default)
    {
        if (rawCandidates is null || rawCandidates.Count == 0)
            return Array.Empty<CandidateTrack>();

        var result = new List<CandidateTrack>();
        var artistTagsCache = new Dictionary<string, WeightedTagVector>(StringComparer.OrdinalIgnoreCase);
        var artistPopCache = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in rawCandidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (!artistTagsCache.TryGetValue(raw.ArtistName, out var tagVector))
                {
                    var rawTags = await _apiClient.GetArtistTopTagsAsync(raw.ArtistName, 15, cancellationToken)
                        .ConfigureAwait(false);

                    var tagWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                    double maxCount = 100.0;

                    foreach (var tag in rawTags)
                    {
                        if (string.IsNullOrWhiteSpace(tag.Name) || tag.Count <= 0)
                            continue;

                        string cleaned = tag.Name.Trim().ToLowerInvariant();
                        if (StoplistTags.Contains(cleaned))
                            continue;

                        double weight = Math.Clamp(tag.Count / maxCount, 0.1, 1.0);
                        tagWeights[cleaned] = weight;
                    }

                    if (tagWeights.Count == 0)
                    {
                        // Fallback generic tag if artist has zero descriptive tags
                        tagWeights["indie"] = 0.5;
                    }

                    tagVector = WeightedTagVector.FromDictionary(tagWeights);
                    artistTagsCache[raw.ArtistName] = tagVector;
                }

                if (!artistPopCache.TryGetValue(raw.ArtistName, out var popularity))
                {
                    // Estimate popularity: fetch top tracks to inspect listener counts
                    var topTracks = await _apiClient.GetArtistTopTracksAsync(raw.ArtistName, 1, cancellationToken)
                        .ConfigureAwait(false);

                    long listeners = 10000; // sensible default
                    if (topTracks.Count > 0 &&
                        !string.IsNullOrWhiteSpace(topTracks[0].Listeners) &&
                        long.TryParse(topTracks[0].Listeners, out var parsedListeners))
                    {
                        listeners = parsedListeners;
                    }

                    popularity = NormalizePopularity(listeners);
                    artistPopCache[raw.ArtistName] = popularity;
                }

                var track = Track.Create(raw.Title, raw.ArtistName, albumTitle: null, mbid: raw.Mbid);
                var candidate = new CandidateTrack(track, tagVector, popularity);
                result.Add(candidate);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to hydrate candidate track '{Title}' by '{Artist}'", raw.Title, raw.ArtistName);
            }
        }

        return result;
    }

    /// <summary>
    /// Logarithmic listener count normalization between [0.05, 0.99]
    /// 100 listeners -> ~0.05 (Underground)
    /// 100,000 listeners -> ~0.50 (Mid-tier indie)
    /// 50,000,000 listeners -> ~0.99 (Mainstream mega-pop)
    /// </summary>
    private static double NormalizePopularity(long listeners)
    {
        if (listeners <= 100) return 0.05;
        double logVal = Math.Log10(listeners);
        // Base range from log10(100) = 2.0 to log10(50,000,000) = 7.7
        double normalized = (logVal - 2.0) / (7.7 - 2.0);
        return Math.Clamp(Math.Round(normalized, 2), 0.05, 0.99);
    }
}
