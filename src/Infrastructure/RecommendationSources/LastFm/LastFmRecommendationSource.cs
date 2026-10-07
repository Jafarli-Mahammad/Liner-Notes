using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Infrastructure.RecommendationSources.LastFm.Models;
using Microsoft.Extensions.Logging;

namespace LinerNotes.Infrastructure.RecommendationSources.LastFm;

/// <summary>Discovery snapshots preserve every supplied path; no match bonus is invented.</summary>
public sealed class LastFmRecommendationSource(ILastFmApiClient apiClient, ILogger<LastFmRecommendationSource> logger) : IRecommendationSource
{
    public string SourceName => "Last.fm";

    public Task<IngestionResult<RawCandidateTrack>> GetCandidatesByArtistsAsync(IReadOnlyList<string> artistNames,
        int limitPerArtist = 10, CancellationToken cancellationToken = default) => Discover(artistNames, limitPerArtist, false, cancellationToken);
    public Task<IngestionResult<RawCandidateTrack>> GetCandidatesByTagsAsync(IReadOnlyList<string> tags,
        int limitPerTag = 10, CancellationToken cancellationToken = default) => Discover(tags, limitPerTag, true, cancellationToken);

    private async Task<IngestionResult<RawCandidateTrack>> Discover(IReadOnlyList<string> seeds, int limit,
        bool byTag, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));
        if (seeds is null || seeds.Count == 0) return new([]);
        var candidates = new Dictionary<string, RawCandidateTrack>(StringComparer.Ordinal);
        var gaps = new List<CoverageGap>();
        var tracksByArtist = new Dictionary<string, LastFmResponse<LastFmTrackItem>>(StringComparer.OrdinalIgnoreCase);
        EvidenceOrigin? origin = null;
        void CheckOrigin(ResponseReference reference)
        {
            origin ??= reference.Origin;
            if (origin != reference.Origin) throw new InvalidOperationException("Mixed provider origins in discovery.");
        }
        static void CheckRequest(ResponseReference reference, string method, string identity)
        {
            if (reference.Provider != "Last.fm" || reference.Method != method ||
                !StringComparer.OrdinalIgnoreCase.Equals(reference.RequestIdentity.Trim(), identity.Trim()))
                throw new InvalidOperationException("Response provenance does not match the requested endpoint/seed.");
        }
        void AddTracks(string seed, LastFmResponse<LastFmTrackItem> response,
            LastFmArtistSummary? similar = null, ResponseReference? similarityResponse = null,
            int? similarityPosition = null)
        {
            CheckOrigin(response.Reference);
            gaps.AddRange(response.Gaps);
            int position = 0;
            foreach (var track in response)
            {
                position++;
                string? artist = track.Artist?.Name;
                // An artist.getTopTracks response is scoped to the requested artist.
                if (string.IsNullOrWhiteSpace(artist) && response.Reference.Method == "artist.gettoptracks")
                    artist = response.Reference.RequestIdentity;
                if (string.IsNullOrWhiteSpace(track.Name) || string.IsNullOrWhiteSpace(artist))
                {
                    gaps.Add(new(seed, response.Reference.Method, "missing_track_or_artist_identity", response.Reference));
                    continue;
                }
                var path = new DiscoveryPath(seed, similar is null ? ObservedValue<double>.Missing("not_supplied_by_endpoint") : LastFmEvidenceParser.Match(similar.Match),
                    similarityResponse, response.Reference, track.ResponsePosition ?? position, track.Url, track.Artist?.Url ??
                    (StringComparer.OrdinalIgnoreCase.Equals(artist, similar?.Name) ? similar?.Url : null),
                    LastFmEvidenceParser.Count(track.Listeners), track.RawRank, similar?.Name, similarityPosition);
                var candidate = new RawCandidateTrack(track.Name, artist, track.Mbid, [path]);
                if (candidates.TryGetValue(candidate.CandidateKey, out var previous))
                    candidate = new(previous.Title, previous.ArtistName, previous.Mbid, previous.Paths.Add(path));
                candidates[candidate.CandidateKey] = candidate;
            }
        }
        foreach (var seed in seeds.OrderBy(s => s?.Trim().ToLowerInvariant(), StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(seed)) { gaps.Add(new("", byTag ? "tag.gettoptracks" : "artist.getsimilar", "invalid_seed")); continue; }
            int pathsBefore = candidates.Values.Sum(candidate => candidate.Paths.Length);
            if (byTag)
            {
                var response = await apiClient.GetTagTopTracksAsync(seed.Trim(), limit, cancellationToken).ConfigureAwait(false);
                CheckRequest(response.Reference, "tag.gettoptracks", seed);
                AddTracks(seed.Trim(), response);
            }
            else
            {
                var similar = await apiClient.GetSimilarArtistsAsync(seed.Trim(), limit, cancellationToken).ConfigureAwait(false);
                CheckRequest(similar.Reference, "artist.getsimilar", seed);
                CheckOrigin(similar.Reference);
                gaps.AddRange(similar.Gaps);
                int similarityPosition = 0;
                foreach (var artist in similar)
                {
                    similarityPosition++;
                    cancellationToken.ThrowIfCancellationRequested();
                    if (string.IsNullOrWhiteSpace(artist.Name))
                    {
                        gaps.Add(new(seed, similar.Reference.Method, "invalid_similar_artist_identity", similar.Reference));
                        continue;
                    }
                    if (!tracksByArtist.TryGetValue(artist.Name.Trim(), out var tracks))
                    {
                        // Existing acquisition bound only; upstream popularity order is retained as provenance.
                        tracks = await apiClient.GetArtistTopTracksAsync(artist.Name.Trim(), 2, cancellationToken).ConfigureAwait(false);
                        CheckRequest(tracks.Reference, "artist.gettoptracks", artist.Name);
                        tracksByArtist[artist.Name.Trim()] = tracks;
                    }
                    AddTracks(seed.Trim(), tracks, artist, similar.Reference, artist.ResponsePosition ?? similarityPosition);
                }
            }
            if (candidates.Values.Sum(candidate => candidate.Paths.Length) == pathsBefore)
                gaps.Add(new(seed.Trim(), byTag ? "tag.gettoptracks" : "artist.getsimilar", "no_supported_tracks"));
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (gaps.Count > 0) logger.LogInformation("Candidate discovery contains explicit coverage gaps.");
        return new(candidates.Values.OrderBy(c => c.CandidateKey, StringComparer.Ordinal), gaps);
    }
}
