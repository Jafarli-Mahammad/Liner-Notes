using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using LinerNotes.Application.Common.Models.Recommendation;

namespace LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;

public enum ListenBrainzClientMode { FixtureOnly, Recorded, LiveOnly }
public enum ListenBrainzRequestKind { ArtistSearch, ArtistRadio, TagRadio, ArtistMetadata, RecordingMetadata }

public sealed class ListenBrainzOptions
{
    public const string SectionName = "ListenBrainz";
    public ListenBrainzClientMode Mode { get; set; } = ListenBrainzClientMode.FixtureOnly;
    public string? Token { get; set; }
    public string UserAgent { get; set; } = "LinerNotes/1.0 (https://github.com/mahammadjafarli/LinerNotes)";
    public bool LiveUseApproved { get; set; }
    public int CacheSeconds { get; set; } = 900;
    public int MaximumRequests { get; set; } = 80;
    // All processes sharing an upstream IP must use the same private, shared directory.
    public string RateLimitDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "linernotes-metabrainz-pacing");

    public void Validate()
    {
        if (!Enum.IsDefined(Mode) || CacheSeconds is < 1 or > 3600 || MaximumRequests is < 1 or > 200 || !Path.IsPathFullyQualified(RateLimitDirectory) ||
            string.IsNullOrWhiteSpace(UserAgent) || UserAgent.Length > 300 || UserAgent.Contains('\r') || UserAgent.Contains('\n') ||
            !UserAgent.Contains('(') || !UserAgent.Contains(')') ||
            (Token is not null && (Token.Length > 200 || Token.Any(char.IsWhiteSpace))))
            throw new ArgumentException("listenbrainz_configuration_invalid");
    }
}

public sealed record ListenBrainzRequest(ListenBrainzRequestKind Kind, string Identity, int Limit = 10)
{
    public string Provider => Kind == ListenBrainzRequestKind.ArtistSearch ? "MusicBrainz" : "ListenBrainz";
    public string Method => Kind switch
    {
        ListenBrainzRequestKind.ArtistSearch => "artist.search",
        ListenBrainzRequestKind.ArtistRadio => "lb-radio.artist",
        ListenBrainzRequestKind.TagRadio => "lb-radio.tags",
        ListenBrainzRequestKind.ArtistMetadata => "metadata.artist",
        ListenBrainzRequestKind.RecordingMetadata => "metadata.recording",
        _ => throw new ArgumentException("listenbrainz_request_invalid")
    };
    public string Key => $"{Kind}:{Limit}:{Identity}";

    public void Validate()
    {
        if (!Enum.IsDefined(Kind) || Limit is < 1 or > 100 || string.IsNullOrWhiteSpace(Identity) || Identity.Length > 4000 ||
            Identity.Any(char.IsControl) || Identity != Identity.Trim())
            throw new ArgumentException("listenbrainz_request_invalid");
        if (Kind is ListenBrainzRequestKind.ArtistRadio or ListenBrainzRequestKind.ArtistMetadata or ListenBrainzRequestKind.RecordingMetadata)
        {
            var ids = Identity.Split(',');
            if (ids.Length > 100 || (Kind == ListenBrainzRequestKind.ArtistRadio && ids.Length != 1) ||
                ids.Any(id => !Guid.TryParseExact(id, "D", out var parsed) || parsed == Guid.Empty))
                throw new ArgumentException("listenbrainz_identifier_invalid");
        }
        else if (Identity.Length > 200) throw new ArgumentException("listenbrainz_seed_limit");
    }

    public string RelativeUri()
    {
        Validate();
        string id = Uri.EscapeDataString(Identity);
        string count = Limit.ToString(CultureInfo.InvariantCulture);
        // Full popularity range: no application popularity filter. Upstream sampling is retained as provenance.
        return Kind switch
        {
            ListenBrainzRequestKind.ArtistSearch => "ws/2/artist/?fmt=json&limit=25&query=" +
                Uri.EscapeDataString("artist:\"" + Identity.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\""),
            ListenBrainzRequestKind.ArtistRadio => $"1/lb-radio/artist/{id}?mode=medium&max_similar_artists={count}&max_recordings_per_artist=2&pop_begin=0&pop_end=100",
            ListenBrainzRequestKind.TagRadio => $"1/lb-radio/tags?tag={id}&operator=OR&count={count}&pop_begin=0&pop_end=100",
            ListenBrainzRequestKind.ArtistMetadata => $"1/metadata/artist/?artist_mbids={id}&inc=tag",
            ListenBrainzRequestKind.RecordingMetadata => $"1/metadata/recording/?recording_mbids={id}&inc=artist%20tag",
            _ => throw new ArgumentException("listenbrainz_request_invalid")
        };
    }
}

public sealed record ListenBrainzResponse(ResponseReference Reference, JsonElement? Data, ImmutableArray<CoverageGap> Gaps)
{
    public const int MaximumBytes = 1_000_000;
    public static ListenBrainzResponse Gap(ListenBrainzRequest request, EvidenceOrigin origin, string reason,
        ResponseReference? received = null)
    {
        var reference = received ?? new(request.Provider, request.Method, request.Identity, origin, null, null);
        return new(reference, null, [new(request.Identity, request.Method, reason, reference)]);
    }

    public static ListenBrainzResponse Parse(ListenBrainzRequest request, ReadOnlyMemory<byte> bytes,
        EvidenceOrigin origin, DateTimeOffset retrievedAt)
    {
        request.Validate();
        if (bytes.Length > MaximumBytes) return Gap(request, origin, "response_size_limit");
        var reference = new ResponseReference(request.Provider, request.Method, request.Identity, origin,
            Convert.ToHexStringLower(SHA256.HashData(bytes.Span)), retrievedAt.ToUniversalTime());
        try
        {
            using var document = JsonDocument.Parse(bytes, new() { MaxDepth = 32 });
            return new(reference, document.RootElement.Clone(), []);
        }
        catch (JsonException) { return Gap(request, origin, "invalid_json", reference); }
    }
}

public interface IListenBrainzApiClient
{
    Task<ListenBrainzResponse> SendAsync(ListenBrainzRequest request, CancellationToken cancellationToken = default);
}
