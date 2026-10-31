using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;
using Xunit;

namespace LinerNotes.Infrastructure.Tests.RecommendationSources;

public sealed class FeedbackArtistIdentityTests
{
    [Fact]
    public async Task UncommonCreditedArtistUsesRecordingIdentityDuringSearchOutage()
    {
        var client = new Client();
        var provider = new ListenBrainzRecommendationProvider(client);
        var seed = await provider.GetArtistFromRecordingAsync(client.Recording, "Uncommon artist", default);
        Assert.Equal(client.Artist, seed.CanonicalArtistId);
        Assert.Equal(1, seed.Vector["jazz"]);
        Assert.Contains(seed.SupportingResponses, r => r.Method == "metadata.recording");
        Assert.DoesNotContain(client.Requests, r => r.Kind == ListenBrainzRequestKind.ArtistSearch);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("ambiguous")]
    [InlineData("mismatched")]
    public async Task MissingOrAmbiguousCreditsDoNotInventAnArtistIdentity(string shape)
    {
        var client = new Client { Shape = shape, SearchUnavailable = false };
        var seed = await new ListenBrainzRecommendationProvider(client).GetArtistFromRecordingAsync(client.Recording, "Uncommon artist", default);
        Assert.Null(seed.CanonicalArtistId);
        Assert.True(seed.Vector.IsEmpty);
        Assert.Contains(client.Requests, r => r.Kind == ListenBrainzRequestKind.ArtistSearch);
    }

    [Fact]
    public async Task TemporaryRecordingFailureIsRetryableAndDoesNotMasqueradeAsMissingIdentity()
    {
        var client = new Client { Unavailable = true };
        var failure = await Assert.ThrowsAsync<DiscoverySourceException>(() =>
            new ListenBrainzRecommendationProvider(client).GetArtistFromRecordingAsync(client.Recording, "Uncommon artist", default));
        Assert.True(failure.Retryable);
        Assert.Equal("source_temporarily_unavailable", failure.Message);
        Assert.Single(client.Requests);
    }

    [Fact]
    public void ResolvedArtistIdentitySurvivesPublicSeedSerialization()
    {
        var artist = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var seed = new LinerNotes.Application.Common.Interfaces.Recommendation.SeedTagSnapshot("Uncommon artist",
            LinerNotes.Domain.Catalog.WeightedTagVector.Empty, [],
            new("listenbrainz", "metadata.artist", artist.ToString("D"), EvidenceOrigin.Live, new string('a', 64), now), null, []) { CanonicalArtistId = artist };
        var document = LinerNotes.Infrastructure.Discovery.PublicEvidenceCodec.EncodeSeed(new string('a', 64), "listenbrainz", seed, now, now.AddMinutes(2));
        Assert.Equal(artist, LinerNotes.Infrastructure.Discovery.PublicEvidenceCodec.DecodeSeed(document).CanonicalArtistId);
    }

    private sealed class Client : IListenBrainzApiClient
    {
        public Guid Recording { get; } = Guid.NewGuid();
        public Guid Artist { get; } = Guid.NewGuid();
        public List<ListenBrainzRequest> Requests { get; } = [];
        public string Shape { get; init; } = "valid";
        public bool Unavailable { get; init; }
        public bool SearchUnavailable { get; init; } = true;
        public Task<ListenBrainzResponse> SendAsync(ListenBrainzRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (Unavailable || request.Kind == ListenBrainzRequestKind.ArtistSearch && SearchUnavailable)
                return Task.FromResult(ListenBrainzResponse.Gap(request, EvidenceOrigin.Live, "upstream_unavailable"));
            object body = request.Kind switch {
                ListenBrainzRequestKind.ArtistSearch => new { count = 0, artists = Array.Empty<object>() },
                ListenBrainzRequestKind.RecordingMetadata when Shape == "missing" => new { },
                ListenBrainzRequestKind.RecordingMetadata => new Dictionary<string, object> {
                    [Recording.ToString("D")] = new { recording = new { name = "Track" }, artist = new {
                        name = Shape == "mismatched" ? "Different artist" : "Uncommon artist",
                        artists = (Shape == "ambiguous" ? new[] { Artist, Guid.NewGuid() } : new[] { Artist })
                            .Select(id => new { artist_mbid = id.ToString("D") }).ToArray() } } },
                ListenBrainzRequestKind.ArtistMetadata => new[] { new { artist_mbid = Artist.ToString("D"),
                    tag = new { artist = new[] { new { tag = "jazz", count = 20 } } } } },
                _ => throw new InvalidOperationException("Unexpected provider request")
            };
            return Task.FromResult(ListenBrainzResponse.Parse(request, JsonSerializer.SerializeToUtf8Bytes(body), EvidenceOrigin.Live, DateTimeOffset.UtcNow)
                with { SharedCacheUntilUtc = DateTimeOffset.UtcNow.AddMinutes(2) });
        }
    }
}
