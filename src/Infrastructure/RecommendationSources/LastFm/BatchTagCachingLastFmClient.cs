using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Infrastructure.RecommendationSources.LastFm.Models;

namespace LinerNotes.Infrastructure.RecommendationSources.LastFm;

/// <summary>One scoped batch shares seed and hydration evidence; gaps are also preserved.</summary>
public sealed class BatchTagCachingLastFmClient(ILastFmApiClient inner) : ILastFmApiClient, ISeedTagSource
{
    private readonly Dictionary<string, LastFmResponse<LastFmTagItem>> tags = new(StringComparer.Ordinal);
    public Task<LastFmResponse<LastFmArtistSummary>> GetSimilarArtistsAsync(string artistName, int limit = 10, CancellationToken cancellationToken = default) =>
        inner.GetSimilarArtistsAsync(artistName, limit, cancellationToken);
    public Task<LastFmResponse<LastFmTrackItem>> GetArtistTopTracksAsync(string artistName, int limit = 5, CancellationToken cancellationToken = default) =>
        inner.GetArtistTopTracksAsync(artistName, limit, cancellationToken);
    public Task<LastFmResponse<LastFmTrackItem>> GetTagTopTracksAsync(string tag, int limit = 10, CancellationToken cancellationToken = default) =>
        inner.GetTagTopTracksAsync(tag, limit, cancellationToken);
    public async Task<LastFmResponse<LastFmTagItem>> GetArtistTopTagsAsync(string artistName, int limit = 20, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string key = artistName.Trim().ToLowerInvariant() + ":" + limit;
        if (tags.TryGetValue(key, out var hit)) return hit;
        var response = await inner.GetArtistTopTagsAsync(artistName, limit, cancellationToken).ConfigureAwait(false);
        tags[key] = response;
        return response;
    }
    public async Task<SeedTagSnapshot> GetAsync(string artistName, CancellationToken cancellationToken = default)
    {
        var evidence = LastFmTagEvidence.Materialize(await GetArtistTopTagsAsync(artistName, 15, cancellationToken).ConfigureAwait(false));
        if (evidence.Response.Provider != "Last.fm" || evidence.Response.Method != "artist.gettoptags" ||
            !StringComparer.OrdinalIgnoreCase.Equals(evidence.Response.RequestIdentity.Trim(), artistName.Trim()))
            throw new InvalidOperationException("Seed tag response provenance does not match the requested artist.");
        return new(artistName, evidence.Vector, evidence.Observations, evidence.Response, evidence.MissingReason, evidence.Gaps);
    }
}
