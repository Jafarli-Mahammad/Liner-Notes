using System.Text.Json.Nodes;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Catalog;
using LinerNotes.Infrastructure.Catalog;
using LinerNotes.Infrastructure.Discovery;
using LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;
using Xunit;

namespace LinerNotes.Infrastructure.Tests.RecommendationSources;

public sealed class RecordingDiscoveryTests
{
    [Fact]
public async Task DirectRecordingTagsAvoidArtistMetadataRequests()
{
var client = new RecordingClient();
var provider = new ListenBrainzRecommendationProvider(client);
var raw = await provider.GetCandidatesByRecordingsAsync([Guid.NewGuid()], 10, default);
var hydrated = await provider.HydrateCandidatesBatchAsync(raw.Items, default);
Assert.Equal(raw.Count, hydrated.Count);
Assert.DoesNotContain(client.Requests, r => r.Kind == ListenBrainzRequestKind.ArtistMetadata);
}
[Theory]
[InlineData("[]")]
[InlineData("[null,17,[],{}, {\"tag\":\"electronic\",\"count\":0}, {\"tag\":\"electronic\",\"count\":-1}]")]
[InlineData("{}")]
[InlineData("[null, {\"tag\":\"vocals\",\"count\":\"bad\"}]")]
public async Task TaglessRecordingRelationshipsSurviveWithoutInventedTags(string tags)
{
var client = new RecordingClient { EmptyArtistTags = true, RecordingTagsJson = tags };
var provider = new ListenBrainzRecommendationProvider(client);
var raw = await provider.GetCandidatesByRecordingsAsync([Guid.NewGuid()], 10, default);
var hydrated = await provider.HydrateCandidatesBatchAsync(raw.Items, default);
Assert.NotEmpty(hydrated.Items);
Assert.Equal(raw.Count, hydrated.Count);
Assert.All(hydrated.Items, item =>
{
Assert.Equal("none", item.TagScope);
Assert.True(item.TagVector.IsEmpty);
Assert.Equal("recording_relationship_without_tags", item.TagMissingReason);
Assert.Equal("metadata.recording", item.TagResponse.Method);
Assert.NotNull(item.TagResponse.RetrievedAtUtc);
Assert.NotNull(item.TagResponse.ResponseSha256);
Assert.All(item.RawCandidate.Paths, path =>
{
Assert.Equal("similar-recordings", path.SimilarityResponse!.Method);
Assert.False(path.Match.IsValid);
});
Assert.Contains(item.Gaps, gap => gap.Reason == "recording_relationship_without_tags");
});
var now = DateTimeOffset.UtcNow;
var pool = new PreparedDiscoveryPool(new string('a', 64), "ListenBrainz", EvidenceOrigin.Synthetic, now, now.AddMinutes(5),
    CatalogText.Version, hydrated.Items, hydrated.Gaps, 1000, new string('b', 64));
var restored = PublicEvidenceCodec.DecodePool(PublicEvidenceCodec.Encode(pool));
Assert.Equal(hydrated.Count, restored.Candidates.Count);
Assert.All(restored.Candidates, item =>
{
Assert.Equal("none", item.TagScope);
Assert.True(item.TagVector.IsEmpty);
Assert.Equal("recording_relationship_without_tags", item.TagMissingReason);
Assert.NotEmpty(item.RawCandidate.Paths);
Assert.Contains(item.Gaps, gap => gap.Reason == item.TagMissingReason);
});
}
[Fact]
public async Task TaglessArtistRadioCandidatesStillAbstain()
{
var client = new RecordingClient { EmptyArtistTags = true, RecordingTagsJson = "[]" };
var provider = new ListenBrainzRecommendationProvider(client);
var raw = await provider.GetCandidatesByCanonicalArtistsAsync([Guid.NewGuid()], 10, default);
Assert.NotEmpty(raw.Items);
var hydrated = await provider.HydrateCandidatesBatchAsync(raw.Items, default);
Assert.Empty(hydrated.Items);
Assert.Contains(hydrated.Gaps, gap => gap.Reason == "no_valid_artist_tag_votes");
}
[Fact]
public async Task MissingRecordingTagsUseArtistScopeOnlyWhenArtistVotesExist()
{
var client = new RecordingClient { RecordingTagsJson = "[]" };
var provider = new ListenBrainzRecommendationProvider(client);
var raw = await provider.GetCandidatesByRecordingsAsync([Guid.NewGuid()], 10, default);
var hydrated = await provider.HydrateCandidatesBatchAsync(raw.Items, default);
Assert.Equal(raw.Count, hydrated.Count);
Assert.All(hydrated.Items, item => Assert.Equal("artist", item.TagScope));
Assert.Contains(client.Requests, r => r.Kind == ListenBrainzRequestKind.ArtistMetadata);
}
[Fact]
    public async Task RecordingTagsSupportCandidatesWithoutArtistTags()
    {
        var client = new RecordingClient { EmptyArtistTags = true };
        var provider = new ListenBrainzRecommendationProvider(client);
        var raw = await provider.GetCandidatesByRecordingsAsync([Guid.NewGuid()], 10, default);
        var hydrated = await provider.HydrateCandidatesBatchAsync(raw.Items, default);
        Assert.Equal(raw.Count, hydrated.Count);
        Assert.NotEmpty(hydrated.Items);
        Assert.All(hydrated.Items, item =>
        {
            Assert.Equal("recording", item.TagScope);
            Assert.False(item.TagVector.IsEmpty);
            Assert.Equal(1, item.TagVector["electronic"]);
        });
    }

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
        public bool EmptyArtistTags { get; init; }
public string? RecordingTagsJson { get; init; }
        public List<ListenBrainzRequest> Requests { get; } = [];
        private readonly FixtureListenBrainzApiClient fixture = new();
        public async Task<ListenBrainzResponse> SendAsync(ListenBrainzRequest request, CancellationToken ct = default)
{
Requests.Add(request);
if (EmptyArtistTags && request.Kind == ListenBrainzRequestKind.ArtistMetadata)
return ListenBrainzResponse.Parse(request, System.Text.Encoding.UTF8.GetBytes("[]"), EvidenceOrigin.Synthetic, DateTimeOffset.UtcNow);
var response = await fixture.SendAsync(request, ct);
if (request.Kind == ListenBrainzRequestKind.RecordingMetadata && RecordingTagsJson is not null)
{
var root = JsonNode.Parse(response.Data!.Value.GetRawText())!;
foreach (string id in request.Identity.Split(','))
root[id]!["tag"]!["recording"] = JsonNode.Parse(RecordingTagsJson);
return ListenBrainzResponse.Parse(request, System.Text.Encoding.UTF8.GetBytes(root.ToJsonString()), EvidenceOrigin.Synthetic, DateTimeOffset.UtcNow);
}
return response;
}
}
}
