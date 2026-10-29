using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Catalog;
namespace LinerNotes.Infrastructure.Discovery;

public static class PublicEvidenceCodec
{
    private sealed record Candidate(string Title, string ArtistName, string? Album, string? Mbid, int? Duration,
        string? Spotify, string? Youtube, IReadOnlyDictionary<string, double> Weights, IReadOnlyList<DiscoveryPath> Paths,
        IReadOnlyList<TagObservation> Tags, ResponseReference Response, string? MissingReason,
        IReadOnlyList<CoverageGap> Gaps, string Normalization, IReadOnlyList<ResponseReference> Supporting, string TagScope = "artist");
    private sealed record Pool(string Key, string Provider, EvidenceOrigin Origin, DateTimeOffset PreparedAtUtc,
        DateTimeOffset? ExpiresAtUtc, string NormalizationVersion, Candidate[] Candidates, CoverageGap[] Gaps,
        long SerializedBytes, string ContentSha256);
    private sealed record Seed(string ArtistName, IReadOnlyDictionary<string, double> Weights, TagObservation[] Tags,
        ResponseReference Response, string? MissingReason, CoverageGap[] Gaps, ResponseReference[] Supporting);
    public const string PoolVersion = "prepared-pool-v3", SeedVersion = "prepared-seed-v2";
    public static PublicEvidenceDocument Encode(PreparedDiscoveryPool pool)
    {
        if (pool.Origin == EvidenceOrigin.Recorded || pool.Candidates.Any(c => c.Origin != pool.Origin ||
            c.TagResponse.Origin != pool.Origin || c.Tags.Any(t => t.Response.Origin != pool.Origin) ||
            c.SupportingResponses.Any(r => r.Origin != pool.Origin) || c.Gaps.Any(g => g.Response is { } r && r.Origin != pool.Origin)) ||
            pool.Gaps.Any(g => g.Response is { } r && r.Origin != pool.Origin))
            throw new GenerationStoppedException("public_evidence_invalid");
        var value = new Pool(pool.Key, pool.Provider, pool.Origin, pool.PreparedAtUtc, pool.ExpiresAtUtc, pool.NormalizationVersion,
            pool.Candidates.Select(c => new Candidate(c.Track.Title, c.Track.ArtistName, c.Track.AlbumTitle, c.Track.Mbid,
                c.Track.DurationSeconds, c.Track.ExternalSpotifyUrl, c.Track.ExternalYoutubeUrl, c.TagVector.Weights, c.RawCandidate.Paths,
                c.Tags, c.TagResponse, c.TagMissingReason, c.Gaps, c.TagNormalizationVersion, c.SupportingResponses, c.TagScope)).ToArray(),
            pool.Gaps.ToArray(), pool.SerializedBytes, pool.ContentSha256);
        return Document(pool.Key, PublicEvidenceKind.Pool, pool.Provider, pool.Origin, PoolVersion, value,
            pool.PreparedAtUtc, pool.ExpiresAtUtc ?? pool.PreparedAtUtc);
    }
    public static PreparedDiscoveryPool DecodePool(PublicEvidenceDocument document)
    {
        var pool = JsonSerializer.Deserialize<Pool>(document.PayloadJson) ?? throw new GenerationStoppedException("public_evidence_invalid");
        if (pool.Key != document.Key || pool.Provider != document.Provider || pool.Origin != document.Origin || pool.Candidates.Length > 400)
            throw new GenerationStoppedException("public_evidence_invalid");
        var candidates = pool.Candidates.Select(c => new HydratedCandidateTrack(
            new Track(c.Title, c.ArtistName, c.Album, c.Mbid, c.Duration, c.Spotify, c.Youtube), WeightedTagVector.FromDictionary(c.Weights),
            new RawCandidateTrack(c.Title, c.ArtistName, c.Mbid, c.Paths), c.Tags, c.Response, c.MissingReason, c.Gaps)
        { TagScope = c.TagScope, TagNormalizationVersion = c.Normalization, SupportingResponses = c.Supporting }).ToArray();
        if (candidates.Any(c => c.Origin != document.Origin || c.Tags.Any(t => t.Response.Origin != document.Origin) ||
            c.SupportingResponses.Any(r => r.Origin != document.Origin))) throw new GenerationStoppedException("public_evidence_invalid");
        var expires = pool.ExpiresAtUtc is { } original && original < document.ExpiresAtUtc ? original : document.ExpiresAtUtc;
        return new(pool.Key, pool.Provider, pool.Origin, pool.PreparedAtUtc, expires, pool.NormalizationVersion,
            candidates, pool.Gaps, pool.SerializedBytes, pool.ContentSha256);
    }
    public static PublicEvidenceDocument EncodeSeed(string key, string provider, SeedTagSnapshot snapshot, DateTimeOffset now, DateTimeOffset? expiry)
    {
        if (snapshot.Response.Origin == EvidenceOrigin.Recorded || snapshot.Tags.Any(t => t.Response.Origin != snapshot.Response.Origin) ||
            snapshot.SupportingResponses.Any(r => r.Origin != snapshot.Response.Origin) || snapshot.Gaps.Any(g => g.Response is { } r && r.Origin != snapshot.Response.Origin))
            throw new GenerationStoppedException("public_evidence_invalid");
        return Document(key, PublicEvidenceKind.Seed, provider, snapshot.Response.Origin, SeedVersion,
            new Seed(snapshot.ArtistName, snapshot.Vector.Weights, snapshot.Tags.ToArray(), snapshot.Response, snapshot.MissingReason,
                snapshot.Gaps.ToArray(), snapshot.SupportingResponses.ToArray()), now, expiry ?? now);
    }
    public static SeedTagSnapshot DecodeSeed(PublicEvidenceDocument document)
    {
        var value = JsonSerializer.Deserialize<Seed>(document.PayloadJson) ?? throw new GenerationStoppedException("public_evidence_invalid");
        if (value.Response.Origin != document.Origin || value.Tags.Any(t => t.Response.Origin != document.Origin) ||
            value.Supporting.Any(r => r.Origin != document.Origin)) throw new GenerationStoppedException("public_evidence_invalid");
        return new(value.ArtistName, WeightedTagVector.FromDictionary(value.Weights), value.Tags, value.Response, value.MissingReason, value.Gaps)
        { SupportingResponses = value.Supporting };
    }
    public static PublicEvidenceDocument Document<T>(string key, PublicEvidenceKind kind, string provider, EvidenceOrigin origin,
        string version, T value, DateTimeOffset acquired, DateTimeOffset expires)
    {
        string json = JsonSerializer.Serialize(value);
        return new(key, kind, provider, origin, version, json, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))), acquired, expires);
    }
}
