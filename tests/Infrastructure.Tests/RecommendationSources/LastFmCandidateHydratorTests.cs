using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Infrastructure.RecommendationSources.LastFm;
using LinerNotes.Infrastructure.RecommendationSources.LastFm.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace LinerNotes.Infrastructure.Tests.RecommendationSources;

public sealed class LastFmCandidateHydratorTests
{
    private readonly ILastFmApiClient _apiClient = Substitute.For<ILastFmApiClient>();

    [Fact]
    public async Task HydrateCandidateAsync_FiltersStoplistTags_AndNormalizesWeights()
    {
        // Arrange
        var rawCandidate = new RawCandidateTrack("Koca Bir Saçmalık", "Jakuzi", "mbid-j", 0.9, "lastfm:artist.getsimilar");

        _apiClient.GetArtistTopTagsAsync("Jakuzi", 15, Arg.Any<CancellationToken>())
            .Returns(new List<LastFmTagItem>
            {
                new("synthpop", 100, null),
                new("darkwave", 80, null),
                new("seen live", 50, null), // Stoplist tag
                new("favorite", 40, null),  // Stoplist tag
                new("post-punk", 60, null)
            });

        _apiClient.GetArtistTopTracksAsync("Jakuzi", 1, Arg.Any<CancellationToken>())
            .Returns(new List<LastFmTrackItem>
            {
                new("Koca Bir Saçmalık", null, null, null, "25000", null, null)
            });

        var hydrator = new LastFmCandidateHydrator(_apiClient, NullLogger<LastFmCandidateHydrator>.Instance);

        // Act
        var candidate = await hydrator.HydrateCandidateAsync(rawCandidate);

        // Assert
        Assert.NotNull(candidate);
        Assert.Equal("Koca Bir Saçmalık", candidate.Track.Title);
        Assert.Equal("Jakuzi", candidate.Track.ArtistName);

        // Verify stoplist tags are excluded
        Assert.False(candidate.TagVector.Weights.ContainsKey("seen live"));
        Assert.False(candidate.TagVector.Weights.ContainsKey("favorite"));

        // Verify descriptive tags are included with normalized weights
        Assert.True(candidate.TagVector.Weights.ContainsKey("synthpop"));
        Assert.Equal(1.0, candidate.TagVector["synthpop"]);
        Assert.True(candidate.TagVector.Weights.ContainsKey("darkwave"));
        Assert.Equal(0.8, candidate.TagVector["darkwave"]);

        // Verify popularity calculation is reasonable
        Assert.True(candidate.GlobalPopularity > 0.0 && candidate.GlobalPopularity < 1.0);
    }

    [Fact]
    public async Task HydrateCandidatesBatchAsync_ReusesFetchedTagVectorForSameArtist()
    {
        // Arrange
        var raw1 = new RawCandidateTrack("Track 1", "Jakuzi", null, 0.9, "src");
        var raw2 = new RawCandidateTrack("Track 2", "Jakuzi", null, 0.8, "src");

        _apiClient.GetArtistTopTagsAsync("Jakuzi", 15, Arg.Any<CancellationToken>())
            .Returns(new List<LastFmTagItem> { new("synthpop", 100, null) });

        _apiClient.GetArtistTopTracksAsync("Jakuzi", 1, Arg.Any<CancellationToken>())
            .Returns(new List<LastFmTrackItem> { new("Top Track", null, null, null, "10000", null, null) });

        var hydrator = new LastFmCandidateHydrator(_apiClient, NullLogger<LastFmCandidateHydrator>.Instance);

        // Act
        var candidates = await hydrator.HydrateCandidatesBatchAsync(new[] { raw1, raw2 });

        // Assert
        Assert.Equal(2, candidates.Count);
        // ApiClient should only have been called ONCE for Jakuzi tags despite 2 tracks
        await _apiClient.Received(1).GetArtistTopTagsAsync("Jakuzi", 15, Arg.Any<CancellationToken>());
    }
}
