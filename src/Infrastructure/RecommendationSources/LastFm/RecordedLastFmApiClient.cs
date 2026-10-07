using System.Collections.Immutable;
using System.Security.Cryptography;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Infrastructure.RecommendationSources.LastFm.Models;

namespace LinerNotes.Infrastructure.RecommendationSources.LastFm;

/// <summary>Caller-supplied replay bytes only. No files, HTTP, credentials or live fallback.</summary>
public sealed class RecordedLastFmResponse
{
    internal ImmutableArray<byte> Body { get; }
    public string Method { get; }
    public string Identity { get; }
    public string Sha256 { get; }
    public DateTimeOffset RetrievedAtUtc { get; }
    public int RecordedLimit { get; }

    public RecordedLastFmResponse(string method, string identity, ReadOnlySpan<byte> body,
        string expectedSha256, DateTimeOffset retrievedAtUtc, int recordedLimit = int.MaxValue)
    {
        if (body.Length > 100_000 || recordedLimit <= 0 || string.IsNullOrWhiteSpace(identity))
            throw new ArgumentException("Invalid or oversized recorded response.");
        Sha256 = Convert.ToHexStringLower(SHA256.HashData(body));
        if (!StringComparer.Ordinal.Equals(Sha256, expectedSha256)) throw new ArgumentException("Recorded response hash mismatch.");
        Method = method.ToLowerInvariant(); Identity = identity.Trim(); Body = ImmutableArray.Create(body.ToArray());
        RetrievedAtUtc = retrievedAtUtc.ToUniversalTime(); RecordedLimit = recordedLimit;
    }
}

public sealed class RecordedLastFmApiClient(IEnumerable<RecordedLastFmResponse> recordings) : ILastFmApiClient
{
    private readonly ImmutableArray<RecordedLastFmResponse> inputs = recordings.ToImmutableArray();
    private readonly Dictionary<string, int> cursors = new(StringComparer.Ordinal);
    private readonly object gate = new();

    public Task<LastFmResponse<LastFmArtistSummary>> GetSimilarArtistsAsync(string artistName, int limit = 10, CancellationToken cancellationToken = default) =>
        Replay<LastFmArtistSummary>("artist.getsimilar", artistName, limit, cancellationToken);
    public Task<LastFmResponse<LastFmTagItem>> GetArtistTopTagsAsync(string artistName, int limit = 20, CancellationToken cancellationToken = default) =>
        Replay<LastFmTagItem>("artist.gettoptags", artistName, limit, cancellationToken);
    public Task<LastFmResponse<LastFmTrackItem>> GetArtistTopTracksAsync(string artistName, int limit = 5, CancellationToken cancellationToken = default) =>
        Replay<LastFmTrackItem>("artist.gettoptracks", artistName, limit, cancellationToken);
    public Task<LastFmResponse<LastFmTrackItem>> GetTagTopTracksAsync(string tag, int limit = 10, CancellationToken cancellationToken = default) =>
        Replay<LastFmTrackItem>("tag.gettoptracks", tag, limit, cancellationToken);

    private Task<LastFmResponse<T>> Replay<T>(string method, string identity, int limit, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));
        identity = identity?.Trim() ?? "";
        var found = inputs.Where(r => r.Method == method && StringComparer.OrdinalIgnoreCase.Equals(r.Identity, identity)).ToArray();
        // Repeated request observations are consumed in their supplied ledger order, not discarded.
        string key = method + "\0" + identity.ToLowerInvariant();
        lock (gate)
        {
            int cursor = cursors.GetValueOrDefault(key);
            if (cursor >= found.Length)
                return Task.FromResult(LastFmEvidenceParser.Gap<T>(new("Last.fm", method, identity, EvidenceOrigin.Recorded, null, null), "recording_request_not_available"));
            var recorded = found[cursor];
            if (limit > recorded.RecordedLimit)
                return Task.FromResult(LastFmEvidenceParser.Gap<T>(new("Last.fm", method, identity, EvidenceOrigin.Recorded,
                    recorded.Sha256, recorded.RetrievedAtUtc), "recorded_limit_insufficient"));
            cursors[key] = cursor + 1;
            return Task.FromResult(LastFmEvidenceParser.Parse<T>(method, recorded.Identity, recorded.Body.AsSpan(),
                EvidenceOrigin.Recorded, recorded.RetrievedAtUtc, limit));
        }
    }
}
