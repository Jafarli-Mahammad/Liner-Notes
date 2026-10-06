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
            .Returns(new List<LastFmArtistSummary>
            {
                new("Son Feci Bisiklet", "mbid-sfb", "0.90", null),
                new("Adamlar", "mbid-adamlar", "0.85", null)
            });

        _apiClient.GetArtistTopTracksAsync("Son Feci Bisiklet", 2, Arg.Any<CancellationToken>())
            .Returns(new List<LastFmTrackItem>
            {
                new("Bu Kız", "mbid-bukiz", null, "200", "50000", null, new LastFmArtistRef("Son Feci Bisiklet", "mbid-sfb", null))
            });

        _apiClient.GetArtistTopTracksAsync("Adamlar", 2, Arg.Any<CancellationToken>())
            .Returns(new List<LastFmTrackItem>
            {
                new("Koca Yaşlı Şişko Dünya", "mbid-dunya", null, "240", "60000", null, new LastFmArtistRef("Adamlar", "mbid-adamlar", null))
            });

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
            .Returns(new List<LastFmArtistSummary> { new("CommonArtist", "mbid-common", "0.9", null) });

        _apiClient.GetSimilarArtistsAsync("Seed2", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<LastFmArtistSummary> { new("CommonArtist", "mbid-common", "0.8", null) });

        _apiClient.GetArtistTopTracksAsync("CommonArtist", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<LastFmTrackItem>
            {
                new("CommonTrack", "mbid-common-track", null, "200", "10000", null, new LastFmArtistRef("CommonArtist", null, null))
            });

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
            .Returns(new List<LastFmTrackItem>
            {
                new("Future Club", "mbid-fc", null, "280", "80000", null, new LastFmArtistRef("Perturbator", null, null)),
                new("Paris", null, null, "270", "60000", null, new LastFmArtistRef("M.O.O.N.", null, null))
            });

        var source = new LastFmRecommendationSource(_apiClient, NullLogger<LastFmRecommendationSource>.Instance);

        // Act
        var candidates = await source.GetCandidatesByTagsAsync(new[] { "synthwave" }, limitPerTag: 5);

        // Assert
        Assert.Equal(2, candidates.Count);
        Assert.Equal("Future Club", candidates[0].Title);
        Assert.Equal("Perturbator", candidates[0].ArtistName);
        Assert.Equal("lastfm:tag.gettoptracks", candidates[0].SourceId);
    }
}
