using System.Net;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Infrastructure.Artwork;
using LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;
using NSubstitute;
using Xunit;

namespace LinerNotes.Infrastructure.Tests;

public sealed class RecordingArtworkTests
{
    private static readonly Guid Recording = Guid.Parse("e97f805a-ab48-4c52-855e-07049142113d");
    private const string Release = "76df3287-6cda-33eb-8e9a-044b5e15ffdd";
    private const string Group = "48140466-cff6-3222-bd55-63c27e43190d";

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public async Task ReleaseThenGroupFallbackIsCached(bool releaseMissing, int expectedCalls)
    {
        using var cache = new RecordingArtworkCache();
        var paths = new List<string>();
        using var http = new HttpClient(new Handler(request =>
        {
            paths.Add(request.RequestUri!.AbsolutePath);
            return releaseMissing && paths.Count == 1 ? new(HttpStatusCode.NotFound) : Image();
        }));
        var (service, metadata, _) = Create(cache, http);
        await service.WarmAsync([Recording], default);
        await service.WarmAsync([Recording], default);
        Assert.Equal(expectedCalls, paths.Count);
        Assert.Equal($"/release/{Release}/front-500", paths[0]);
        if (releaseMissing) Assert.Equal($"/release-group/{Group}/front-500", paths[1]);
        Assert.Equal("image/jpeg", service.GetCached(Recording)!.ContentType);
        await metadata.Received(1).SendAsync(Arg.Any<ListenBrainzRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MissingCoversAreNegativeCachedAndReadsNeverFetch()
    {
        using var cache = new RecordingArtworkCache();
        int calls = 0;
        using var http = new HttpClient(new Handler(_ => { calls++; return new(HttpStatusCode.NotFound); }));
        var (service, metadata, _) = Create(cache, http);
        Assert.Null(service.GetCached(Recording));
        await metadata.DidNotReceive().SendAsync(Arg.Any<ListenBrainzRequest>(), Arg.Any<CancellationToken>());
        await service.WarmAsync([Recording], default);
        await service.WarmAsync([Recording], default);
        Assert.Null(service.GetCached(Recording));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task MetadataIsBatchedAndUsesReleaseInclude()
    {
        using var cache = new RecordingArtworkCache();
        using var http = new HttpClient(new Handler(_ => Image()));
        var (service, metadata, _) = Create(cache, http);
        var other = Guid.NewGuid();
        await service.WarmAsync([Recording, other, Recording, Guid.Empty], default);
        await metadata.Received(1).SendAsync(Arg.Is<ListenBrainzRequest>(request =>
            request.Kind == ListenBrainzRequestKind.ArtworkMetadata &&
            request.Identity == Recording + "," + other && request.RelativeUri().EndsWith("&inc=release")), Arg.Any<CancellationToken>());
        Assert.Null(service.GetCached(other));
        Assert.Throws<ArgumentException>(() => new ListenBrainzRequest(ListenBrainzRequestKind.ArtworkMetadata, "not-an-id").Validate());
    }

    [Theory]
    [InlineData("http://ia800100.us.archive.org/download/cover.jpg", true)]
    [InlineData("https://example.org/cover.jpg", false)]
    [InlineData("http://127.0.0.1/cover.jpg", false)]
    [InlineData("https://archive.org.evil.test/cover.jpg", false)]
    [InlineData("https://archive.org:8443/cover.jpg", false)]
    public async Task RedirectsOnlyReachKnownArchiveHosts(string destination, bool allowed)
    {
        using var cache = new RecordingArtworkCache();
        var uris = new List<Uri>();
        using var http = new HttpClient(new Handler(request =>
        {
            uris.Add(request.RequestUri!);
            if (request.RequestUri!.Host != "coverartarchive.org") return Image();
            var response = new HttpResponseMessage(HttpStatusCode.TemporaryRedirect);
            response.Headers.Location = new Uri(destination);
            return response;
        }));
        var (service, _, _) = Create(cache, http);
        await service.WarmAsync([Recording], default);
        Assert.Equal(allowed, service.GetCached(Recording) is not null);
        Assert.All(uris, uri => Assert.Equal("https", uri.Scheme));
        if (!allowed) Assert.All(uris, uri => Assert.Equal("coverartarchive.org", uri.Host));
    }

    [Theory]
    [InlineData("image/svg+xml", false)]
    [InlineData("image/jpeg", false)]
    [InlineData("image/jpeg", true)]
    public async Task RejectsInvalidAndOversizedImages(string type, bool oversized)
    {
        using var cache = new RecordingArtworkCache();
        using var http = new HttpClient(new Handler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(oversized ? new byte[1024 * 1024 + 1] : "<svg/>"u8.ToArray())
            };
            response.Content.Headers.ContentType = new(type);
            return response;
        }));
        var (service, _, _) = Create(cache, http);
        await service.WarmAsync([Recording], default);
        Assert.Null(service.GetCached(Recording));
    }

    [Fact]
    public async Task RateLimitIsDeferredAndCancellationPropagates()
    {
        using var cache = new RecordingArtworkCache();
        using var http = new HttpClient(new Handler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new(TimeSpan.FromSeconds(45));
            return response;
        }));
        var (service, _, gate) = Create(cache, http);
        await service.WarmAsync([Recording], default);
        await gate.Received().DeferAsync("CoverArtArchive", TimeSpan.FromSeconds(45), Arg.Any<CancellationToken>());
        Assert.Null(service.GetCached(Recording));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.WarmAsync([Guid.NewGuid()], cancelled.Token));
    }

    private static (RecordingArtwork Service, IListenBrainzApiClient Metadata, IMetaBrainzRequestGate Gate)
        Create(RecordingArtworkCache cache, HttpClient http)
    {
        var metadata = Substitute.For<IListenBrainzApiClient>();
        metadata.SendAsync(Arg.Any<ListenBrainzRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
            ListenBrainzResponse.Parse(call.Arg<ListenBrainzRequest>(), System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
                new Dictionary<string, object> { [Recording.ToString()] = new { release = new { mbid = Release, release_group_mbid = Group } } }),
                EvidenceOrigin.Live, DateTimeOffset.UtcNow));
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("CoverArtArchive").Returns(_ => new HttpClient(new Forward(http)));
        var gate = Substitute.For<IMetaBrainzRequestGate>();
        return (new(cache, metadata, factory, gate, new() { Mode = ListenBrainzClientMode.LiveOnly, LiveUseApproved = true }, TimeProvider.System), metadata, gate);
    }

    private static HttpResponseMessage Image()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([0xff, 0xd8, 0xff, 0xd9]) };
        response.Content.Headers.ContentType = new("image/jpeg");
        return response;
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(send(request));
    }

    private sealed class Forward(HttpClient http) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var copy = new HttpRequestMessage(request.Method, request.RequestUri);
            foreach (var header in request.Headers) copy.Headers.TryAddWithoutValidation(header.Key, header.Value);
            return await http.SendAsync(copy, cancellationToken);
        }
    }
}
