using LinerNotes.Infrastructure.Tests.Fakes;
using LinerNotes.Infrastructure.RecommendationSources.LastFm;
using LinerNotes.Infrastructure.RecommendationSources.LastFm.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace LinerNotes.Infrastructure.Tests.RecommendationSources;

public sealed class LastFmRecommendationSourceTests
{
    private readonly ILastFmApiClient _apiClient = Substitute.For<ILastFmApiClient>();

    [Fact]
    public async Task GetCandidatesByArtistsAsync_DiscoversTracksForSimilarArtists()
    {
        // Arrange
        _apiClient.GetSimilarArtistsAsync("Jakuzi", 5, Arg.Any<CancellationToken>())
            .Returns(EvidenceFixtures.Response(new List<LastFmArtistSummary>
            {
                new("Son Feci Bisiklet", "mbid-sfb", "0.90", null),
                new("Adamlar", "mbid-adamlar", "0.85", null)
            }, identity: "Jakuzi", method: "artist.getsimilar"));

        _apiClient.GetArtistTopTracksAsync("Son Feci Bisiklet", 2, Arg.Any<CancellationToken>())
            .Returns(EvidenceFixtures.Response(new List<LastFmTrackItem>
            {
                new("Bu Kız", "mbid-bukiz", null, "200", "50000", null, new LastFmArtistRef("Son Feci Bisiklet", "mbid-sfb", null))
            }, identity: "Son Feci Bisiklet", method: "artist.gettoptracks"));

        _apiClient.GetArtistTopTracksAsync("Adamlar", 2, Arg.Any<CancellationToken>())
            .Returns(EvidenceFixtures.Response(new List<LastFmTrackItem>
            {
                new("Koca Yaşlı Şişko Dünya", "mbid-dunya", null, "240", "60000", null, new LastFmArtistRef("Adamlar", "mbid-adamlar", null))
            }, identity: "Adamlar", method: "artist.gettoptracks"));

        var source = new LastFmRecommendationSource(_apiClient, NullLogger<LastFmRecommendationSource>.Instance);

        // Act
        var candidates = await source.GetCandidatesByArtistsAsync(new[] { "Jakuzi" }, limitPerArtist: 5);

        // Assert
        Assert.Equal(2, candidates.Count);
        Assert.Contains(candidates, c => c.Title == "Bu Kız" && c.ArtistName == "Son Feci Bisiklet" && c.UpstreamScore == 0.90);
        Assert.Contains(candidates, c => c.Title == "Koca Yaşlı Şişko Dünya" && c.ArtistName == "Adamlar" && c.UpstreamScore == 0.85);
    }

    [Fact]
    public async Task GetCandidatesByArtistsAsync_DeduplicatesAcrossMultipleSeeds()
    {
        // Arrange: both seeds return the same similar artist and track
        _apiClient.GetSimilarArtistsAsync("Seed1", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(EvidenceFixtures.Response(new List<LastFmArtistSummary> { new("CommonArtist", "mbid-common", "0.9", null) }, identity: "Seed1", method: "artist.getsimilar"));

        _apiClient.GetSimilarArtistsAsync("Seed2", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(EvidenceFixtures.Response(new List<LastFmArtistSummary> { new("CommonArtist", "mbid-common", "0.8", null) }, identity: "Seed2", method: "artist.getsimilar"));

        _apiClient.GetArtistTopTracksAsync("CommonArtist", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(EvidenceFixtures.Response(new List<LastFmTrackItem>
            {
                new("CommonTrack", "mbid-common-track", null, "200", "10000", null, new LastFmArtistRef("CommonArtist", null, null))
            }, identity: "CommonArtist", method: "artist.gettoptracks"));

        var source = new LastFmRecommendationSource(_apiClient, NullLogger<LastFmRecommendationSource>.Instance);

        // Act
        var candidates = await source.GetCandidatesByArtistsAsync(new[] { "Seed1", "Seed2" });

        // Assert - should contain only 1 instance of CommonTrack
        Assert.Single(candidates);
        Assert.Equal("CommonTrack", candidates[0].Title);
    }

    [Fact]
    public async Task GetCandidatesByTagsAsync_DiscoversTracksForTags()
    {
        // Arrange
        _apiClient.GetTagTopTracksAsync("synthwave", 5, Arg.Any<CancellationToken>())
            .Returns(EvidenceFixtures.Response(new List<LastFmTrackItem>
            {
                new("Future Club", "mbid-fc", null, "280", "80000", null, new LastFmArtistRef("Perturbator", null, null)),
                new("Paris", null, null, "270", "60000", null, new LastFmArtistRef("M.O.O.N.", null, null))
            }, identity: "synthwave", method: "tag.gettoptracks"));

        var source = new LastFmRecommendationSource(_apiClient, NullLogger<LastFmRecommendationSource>.Instance);

        // Act
        var candidates = await source.GetCandidatesByTagsAsync(new[] { "synthwave" }, limitPerTag: 5);

        // Assert
        Assert.Equal(2, candidates.Count);
        Assert.Contains(candidates, candidate => candidate.Title == "Future Club" && candidate.ArtistName == "Perturbator");
        Assert.Contains(candidates, candidate => candidate.Title == "Paris" && candidate.ArtistName == "M.O.O.N.");
        Assert.All(candidates, candidate => Assert.Equal("lastfm:tag.gettoptracks", candidate.SourceId));
        Assert.All(candidates, candidate => Assert.Null(candidate.UpstreamScore));
    }
}
