using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Catalog;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Catalog;
using LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;
using NSubstitute;
using Xunit;

namespace LinerNotes.Infrastructure.Tests;

public sealed class LiveRecordingCatalogTests
{
    private const string Artist = "11111111-1111-4111-8111-111111111111";
    private const string Recording = "22222222-2222-4222-8222-222222222222";

    [Theory]
    [InlineData(EvidenceOrigin.Live, true, true, 1)]
    [InlineData(EvidenceOrigin.Live, false, true, 0)]
    [InlineData(EvidenceOrigin.Recorded, true, true, 0)]
    [InlineData(EvidenceOrigin.Synthetic, true, true, 0)]
    [InlineData(EvidenceOrigin.Live, true, false, 0)]
    public async Task PublicCatalog_RequiresRetainableLiveCoreMetadata(EvidenceOrigin origin,
        bool retainable, bool namedArtist, int expected)
    {
        var now = DateTimeOffset.UtcNow;
        var client = Substitute.For<IListenBrainzApiClient>();
        client.SendAsync(Arg.Any<ListenBrainzRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var request = call.Arg<ListenBrainzRequest>();
            byte[] body = request.Kind == ListenBrainzRequestKind.TagRadio
                ? JsonSerializer.SerializeToUtf8Bytes(new[] { new { recording_mbid = Recording } })
                : JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
                {
                    [Recording] = new
                    {
                        recording = new { name = "Public recording" },
                        artist = new { name = "Public artist", artists = new[] { new { artist_mbid = Artist, name = namedArtist ? "Public artist" : null } } },
                        tag = new { recording = new[] { new { tag = "supplementary-data-must-stay-out", count = 10 } } }
                    }
                });
            return Task.FromResult(ListenBrainzResponse.Parse(request, body, origin, now) with
            {
                SharedCacheUntilUtc = retainable ? now.AddMinutes(10) : null
            });
        });
        var provider = new ListenBrainzRecommendationProvider(client);
        await provider.GetCandidatesByTagsAsync(["rock"], 1);
        var catalog = ((IPublicRecordingCatalogSource)provider).ReadPublicRecordingMetadata();
        Assert.Equal(expected, catalog.Count);
        if (expected == 0) return;
        var publication = Assert.Single(catalog);
        Assert.Equal(CatalogOrigin.Public, publication.Origin);
        Assert.Equal(now.AddMinutes(10), publication.CacheUntilUtc);
        Assert.Equal([Guid.Parse(Recording)], publication.MatchedCanonicalIds);
        Assert.Equal(2, publication.Records.Count);
        Assert.All(publication.Records, row => Assert.Empty(row.Tags));
        var recording = Assert.Single(publication.Records, row => row.Kind == CatalogKind.Track);
        Assert.Equal("Public recording", recording.Name);
        Assert.Equal([Guid.Parse(Artist)], recording.ArtistIds);
        Assert.DoesNotContain("supplementary-data-must-stay-out", JsonSerializer.Serialize(publication));
        Assert.DoesNotContain("rock", JsonSerializer.Serialize(publication));
    }
}
