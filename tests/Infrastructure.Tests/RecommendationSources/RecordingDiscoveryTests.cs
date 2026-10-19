using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Catalog;
using LinerNotes.Infrastructure.Catalog;
using LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;
using Xunit;

namespace LinerNotes.Infrastructure.Tests.RecommendationSources;

public sealed class RecordingDiscoveryTests
{
    [Fact]
    public async Task RecordingSimilarityUsesDirectRecordingEndpointAndKeepsItsIdentity()
    {
        var client = new RecordingClient(); var provider = new ListenBrainzRecommendationProvider(client);
        var recording = Guid.NewGuid();
        var raw = await provider.GetCandidatesByRecordingsAsync([recording], 10, default);
        Assert.NotEmpty(raw.Items);
        Assert.All(raw.Items, candidate => Assert.All(candidate.Paths, path => Assert.Equal(recording.ToString("D"), path.Seed)));
        var hydrated = await provider.HydrateCandidatesBatchAsync(raw.Items, default);
        Assert.NotEmpty(hydrated.Items);
        Assert.All(hydrated.Items, item => Assert.Equal("recording", item.TagScope));
        Assert.DoesNotContain(client.Requests, r => r.Kind is ListenBrainzRequestKind.ArtistRadio or ListenBrainzRequestKind.ArtistSearch);
        Assert.Contains(client.Requests, r => r.Kind == ListenBrainzRequestKind.SimilarRecordings && r.Identity == recording.ToString("D"));
    }

    [Fact]
    public async Task MissingDirectRecordingTagsAreACoverageGapWithoutArtistSubstitution()
    {
        var client = new RecordingClient();
        var snapshot = await new ListenBrainzRecommendationProvider(client).GetRecordingAsync(Guid.NewGuid(), "Fixture recording", default);
        Assert.Empty(snapshot.Vector.Weights); Assert.Equal("direct_recording_tags_missing", snapshot.MissingReason);
        Assert.Equal(ListenBrainzRequestKind.RecordingMetadata, Assert.Single(client.Requests).Kind);
    }

    [Fact]
    public async Task ExplicitRecordingLookupReturnsCanonicalRecordingAndArtistCredits()
    {
        var client = new RecordingClient();
        var result = await new MusicBrainzCatalogLookupSource(client, TimeProvider.System).SearchAsync("Fixture version", CatalogKind.Track, default);
        var recording = Assert.Single(result.Records.Where(r => r.Kind == CatalogKind.Track));
        Assert.Equal("Fixture version", recording.Name); Assert.NotEmpty(recording.ArtistIds);
        Assert.All(recording.ArtistIds, id => Assert.Contains(result.Records, r => r.Kind == CatalogKind.Artist && r.Id == id));
        Assert.Equal(CatalogOrigin.Synthetic, result.Origin);
        Assert.Equal(ListenBrainzRequestKind.RecordingSearch, Assert.Single(client.Requests).Kind);
    }

    [Fact]
    public void RecordingRequestsKeepVersionedIdentityAndUseOnlyAllowlistedRoutes()
    {
        var id = Guid.NewGuid();
        var request = new ListenBrainzRequest(ListenBrainzRequestKind.SimilarRecordings, id.ToString("D"));
        Assert.StartsWith("similar-recordings/json?recording_mbids=", request.RelativeUri());
        Assert.Contains("algorithm=session_based_", request.RelativeUri());
        Assert.Throws<ArgumentException>(() => new ListenBrainzRequest(ListenBrainzRequestKind.SimilarRecordings, "artist name").Validate());
        var search = new ListenBrainzRequest(ListenBrainzRequestKind.RecordingSearch, "title OR artist:any");
        Assert.Equal("MusicBrainz", search.Provider); Assert.StartsWith("ws/2/recording/", search.RelativeUri());
    }

    private sealed class RecordingClient : IListenBrainzApiClient
    {
        public List<ListenBrainzRequest> Requests { get; } = [];
        private readonly FixtureListenBrainzApiClient fixture = new();
        public Task<ListenBrainzResponse> SendAsync(ListenBrainzRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return fixture.SendAsync(request, cancellationToken);
        }
    }
}
