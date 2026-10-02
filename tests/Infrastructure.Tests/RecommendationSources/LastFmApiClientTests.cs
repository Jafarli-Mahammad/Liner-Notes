using System.Net;
using LinerNotes.Infrastructure.RecommendationSources.LastFm;
using LinerNotes.Infrastructure.Tests.Fakes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LinerNotes.Infrastructure.Tests.RecommendationSources;

public sealed class LastFmApiClientTests
{
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private readonly LastFmRateLimiter _rateLimiter = new(requestsPerSecond: 100);

    [Fact]
    public async Task GetSimilarArtistsAsync_ParsesValidResponse_AndConstructsCorrectQuery()
    {
        // Arrange
        string sampleJson = """
        {
            "similarartists": {
                "artist": [
                    { "name": "Son Feci Bisiklet", "mbid": "mbid-sfb", "match": "0.95", "url": "https://last.fm/sfb" },
                    { "name": "Adamlar", "match": "0.80" }
                ],
                "@attr": { "artist": "Jakuzi" }
            }
        }
        """;

        var fakeHandler = MockHttpMessageHandler.WithJsonResponse(sampleJson);
        var httpClient = new HttpClient(fakeHandler)
        {
            BaseAddress = new Uri("https://ws.audioscrobbler.com/2.0/")
        };

        var options = Options.Create(new LastFmOptions
        {
            ApiKey = "test-api-key",
            Mode = LastFmClientMode.LiveOnly
        });

        var client = new LastFmApiClient(httpClient, options, _cache, _rateLimiter, NullLogger<LastFmApiClient>.Instance);

        // Act
        var result = await client.GetSimilarArtistsAsync("Jakuzi", limit: 5);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("Son Feci Bisiklet", result[0].Name);
        Assert.Equal("mbid-sfb", result[0].Mbid);
        Assert.Equal("0.95", result[0].Match);
        Assert.Equal("Adamlar", result[1].Name);

        // Verify outgoing request
        Assert.Single(fakeHandler.RecordedRequests);
        var request = fakeHandler.RecordedRequests[0];
        Assert.Contains("method=artist.getsimilar", request.RequestUri!.Query);
        Assert.Contains("artist=Jakuzi", request.RequestUri.Query);
        Assert.Contains("api_key=test-api-key", request.RequestUri.Query);
    }

    [Fact]
    public async Task GetSimilarArtistsAsync_ServesFromCacheOnSecondCall()
    {
        // Arrange
        string sampleJson = """
        {
            "similarartists": {
                "artist": [
                    { "name": "Son Feci Bisiklet", "match": "0.9" }
                ]
            }
        }
        """;

        var fakeHandler = MockHttpMessageHandler.WithJsonResponse(sampleJson);
        var httpClient = new HttpClient(fakeHandler)
        {
            BaseAddress = new Uri("https://ws.audioscrobbler.com/2.0/")
        };

        var options = Options.Create(new LastFmOptions
        {
            ApiKey = "test-api-key",
            Mode = LastFmClientMode.LiveOnly
        });

        var client = new LastFmApiClient(httpClient, options, _cache, _rateLimiter, NullLogger<LastFmApiClient>.Instance);

        // Act
        var call1 = await client.GetSimilarArtistsAsync("Jakuzi", 5);
        var call2 = await client.GetSimilarArtistsAsync("Jakuzi", 5);

        // Assert
        Assert.Single(call1);
        Assert.Single(call2);
        Assert.Single(fakeHandler.RecordedRequests); // Only 1 HTTP call made, 2nd was cached
    }

    [Fact]
    public async Task GetArtistTopTagsAsync_ParsesTagsAndCountsCorrectly()
    {
        // Arrange
        string sampleJson = """
        {
            "toptags": {
                "tag": [
                    { "name": "synthpop", "count": 100 },
                    { "name": "darkwave", "count": "85" },
                    { "name": "turkish", "count": 60 }
                ]
            }
        }
        """;

        var fakeHandler = MockHttpMessageHandler.WithJsonResponse(sampleJson);
        var httpClient = new HttpClient(fakeHandler) { BaseAddress = new Uri("https://ws.audioscrobbler.com/2.0/") };
        var options = Options.Create(new LastFmOptions { ApiKey = "test-key", Mode = LastFmClientMode.LiveOnly });
        var client = new LastFmApiClient(httpClient, options, _cache, _rateLimiter, NullLogger<LastFmApiClient>.Instance);

        // Act
        var result = await client.GetArtistTopTagsAsync("Jakuzi", 3);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("synthpop", result[0].Name);
        Assert.Equal(100, result[0].Count);
        Assert.Equal("darkwave", result[1].Name);
        Assert.Equal(85, result[1].Count);
    }

    [Fact]
    public async Task GetArtistTopTracksAsync_ParsesTrackItemCorrectly()
    {
        // Arrange
        string sampleJson = """
        {
            "toptracks": {
                "track": [
                    {
                        "name": "Koca Bir Saçmalık",
                        "listeners": "25000",
                        "playcount": "150000",
                        "artist": { "name": "Jakuzi", "mbid": "mbid-j" }
                    }
                ]
            }
        }
        """;

        var fakeHandler = MockHttpMessageHandler.WithJsonResponse(sampleJson);
        var httpClient = new HttpClient(fakeHandler) { BaseAddress = new Uri("https://ws.audioscrobbler.com/2.0/") };
        var options = Options.Create(new LastFmOptions { ApiKey = "test-key", Mode = LastFmClientMode.LiveOnly });
        var client = new LastFmApiClient(httpClient, options, _cache, _rateLimiter, NullLogger<LastFmApiClient>.Instance);

        // Act
        var result = await client.GetArtistTopTracksAsync("Jakuzi", 2);

        // Assert
        Assert.Single(result);
        Assert.Equal("Koca Bir Saçmalık", result[0].Name);
        Assert.Equal("25000", result[0].Listeners);
        Assert.Equal("Jakuzi", result[0].Artist?.Name);
    }

    [Fact]
    public async Task WhenInFixtureOnlyMode_NeverMakesOutboundHttpRequests()
    {
        // Arrange
        var fakeHandler = MockHttpMessageHandler.WithStatusCode(HttpStatusCode.InternalServerError);
        var httpClient = new HttpClient(fakeHandler) { BaseAddress = new Uri("https://ws.audioscrobbler.com/2.0/") };
        var options = Options.Create(new LastFmOptions
        {
            Mode = LastFmClientMode.FixtureOnly
        });
        var client = new LastFmApiClient(httpClient, options, _cache, _rateLimiter, NullLogger<LastFmApiClient>.Instance);

        // Act
        var result = await client.GetSimilarArtistsAsync("Jakuzi", 5);

        // Assert
        Assert.NotEmpty(result);
        Assert.Equal("Son Feci Bisiklet", result[0].Name); // From fixture
        Assert.Empty(fakeHandler.RecordedRequests); // Zero HTTP requests dispatched
    }

    [Fact]
    public async Task WhenHttpFails_FallsBackToFixturesInHybridMode()
    {
        // Arrange
        var fakeHandler = MockHttpMessageHandler.WithStatusCode(HttpStatusCode.TooManyRequests);
        var httpClient = new HttpClient(fakeHandler) { BaseAddress = new Uri("https://ws.audioscrobbler.com/2.0/") };
        var options = Options.Create(new LastFmOptions
        {
            ApiKey = "test-key",
            Mode = LastFmClientMode.Hybrid
        });
        var client = new LastFmApiClient(httpClient, options, _cache, _rateLimiter, NullLogger<LastFmApiClient>.Instance);

        // Act
        var result = await client.GetSimilarArtistsAsync("Jakuzi", 3);

        // Assert
        Assert.NotEmpty(result); // Successfully falls back to offline fixture
        Assert.Equal("Son Feci Bisiklet", result[0].Name);
        Assert.Single(fakeHandler.RecordedRequests); // Attempted HTTP once, failed with 429, fell back safely
    }
}
