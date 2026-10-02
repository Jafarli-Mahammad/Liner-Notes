using System.Globalization;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using Microsoft.Extensions.Logging;

namespace LinerNotes.Infrastructure.RecommendationSources.LastFm;

/// <summary>
/// Upstream recommendation source discovering raw candidate tracks via Last.fm Web Services.
/// Discovers candidates using artist.getSimilar and tag.getTopTracks.
/// </summary>
public sealed class LastFmRecommendationSource : IRecommendationSource
{
    private readonly ILastFmApiClient _apiClient;
    private readonly ILogger<LastFmRecommendationSource> _logger;

    public string SourceName => "Last.fm";

    public LastFmRecommendationSource(
        ILastFmApiClient apiClient,
        ILogger<LastFmRecommendationSource> logger)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<RawCandidateTrack>> GetCandidatesByArtistsAsync(
        IReadOnlyList<string> artistNames,
        int limitPerArtist = 10,
        CancellationToken cancellationToken = default)
    {
        if (artistNames is null || artistNames.Count == 0)
            return Array.Empty<RawCandidateTrack>();

        var candidates = new List<RawCandidateTrack>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var seedArtist in artistNames)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(seedArtist))
                continue;

            try
            {
                var similarArtists = await _apiClient.GetSimilarArtistsAsync(seedArtist.Trim(), limitPerArtist, cancellationToken)
                    .ConfigureAwait(false);

                foreach (var similar in similarArtists)
                {
                    double matchScore = 0.5;
                    if (!string.IsNullOrWhiteSpace(similar.Match) &&
                        double.TryParse(similar.Match, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedMatch))
                    {
                        matchScore = Math.Clamp(parsedMatch, 0.0, 1.0);
                    }

                    // Retrieve top 2 representative tracks for the discovered artist
                    var topTracks = await _apiClient.GetArtistTopTracksAsync(similar.Name, 2, cancellationToken)
                        .ConfigureAwait(false);

                    if (topTracks.Count == 0)
                    {
                        // Fallback placeholder track if no top tracks returned
                        string key = $"{similar.Name.ToLowerInvariant()}:top-pick";
                        if (seenKeys.Add(key))
                        {
                            candidates.Add(new RawCandidateTrack(
                                Title: $"{similar.Name} Top Pick",
                                ArtistName: similar.Name,
                                Mbid: similar.Mbid,
                                UpstreamScore: matchScore,
                                SourceId: "lastfm:artist.getsimilar"));
                        }
                    }
                    else
                    {
                        foreach (var track in topTracks)
                        {
                            string key = !string.IsNullOrEmpty(track.Mbid)
                                ? $"mbid:{track.Mbid.ToLowerInvariant()}"
                                : $"{similar.Name.ToLowerInvariant()}:{track.Name.ToLowerInvariant()}";

                            if (seenKeys.Add(key))
                            {
                                candidates.Add(new RawCandidateTrack(
                                    Title: track.Name,
                                    ArtistName: similar.Name,
                                    Mbid: track.Mbid ?? similar.Mbid,
                                    UpstreamScore: matchScore,
                                    SourceId: "lastfm:artist.getsimilar"));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error discovering candidates for seed artist '{Artist}'", seedArtist);
            }
        }

        return candidates;
    }

    public async Task<IReadOnlyList<RawCandidateTrack>> GetCandidatesByTagsAsync(
        IReadOnlyList<string> tags,
        int limitPerTag = 10,
        CancellationToken cancellationToken = default)
    {
        if (tags is null || tags.Count == 0)
            return Array.Empty<RawCandidateTrack>();

        var candidates = new List<RawCandidateTrack>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in tags)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(tag))
                continue;

            try
            {
                var topTracks = await _apiClient.GetTagTopTracksAsync(tag.Trim(), limitPerTag, cancellationToken)
                    .ConfigureAwait(false);

                foreach (var track in topTracks)
                {
                    string artistName = track.Artist?.Name ?? "Unknown Artist";
                    string key = !string.IsNullOrEmpty(track.Mbid)
                        ? $"mbid:{track.Mbid.ToLowerInvariant()}"
                        : $"{artistName.ToLowerInvariant()}:{track.Name.ToLowerInvariant()}";

                    if (seenKeys.Add(key))
                    {
                        candidates.Add(new RawCandidateTrack(
                            Title: track.Name,
                            ArtistName: artistName,
                            Mbid: track.Mbid,
                            UpstreamScore: 0.70,
                            SourceId: "lastfm:tag.gettoptracks"));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error discovering candidates for seed tag '{Tag}'", tag);
            }
        }

        return candidates;
    }
}
