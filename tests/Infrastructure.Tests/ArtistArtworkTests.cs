using System.Net;
using System.Net.Http.Headers;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Infrastructure.Artwork;
using LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;
using NSubstitute;
using Xunit;

namespace LinerNotes.Infrastructure.Tests;

public sealed class ArtistArtworkTests
{
    private static readonly Guid Artist = Guid.Parse("d67d5d3a-7b59-4460-b09f-49efe00937e7");

    [Theory]
    [InlineData("headerless-image-with-age", true)]
    [InlineData("expires-image-with-age", true)]
    [InlineData("stale-image", false)]
    [InlineData("stale-metadata", false)]
    public async Task ArtworkExpiryUsesTheOriginLifetimeWithoutExpiringHeaderlessMedia(string mode, bool available)
    {
        var photo = await AcquireAsync("CC0", [], mode: mode);
        Assert.Equal(available, photo is not null);
        if (photo is not null) Assert.InRange(photo.ExpiresAtUtc, DateTimeOffset.UtcNow.AddMinutes(1), DateTimeOffset.UtcNow.AddMinutes(11));
    }

    [Fact]
    public void ExpiredAcquisitionCannotCauseAnImmediateRequeueLoop()
    {
        using var caches = new LinerNotes.Infrastructure.Caching.DiscoveryMemoryCaches(new());
        var cache = new ArtistArtworkCache(TimeProvider.System, caches);
        cache.Complete(Artist, new ArtistPhoto([], "image/jpeg", "https://commons.wikimedia.org/wiki/File:Example.jpg", "Photographer", "CC0", "", DateTimeOffset.UtcNow.AddMinutes(-1)));
        Assert.Null(cache.GetCached(Artist));
        Assert.Equal(RecordingArtworkState.Unavailable, cache.Request(Artist));
    }

    [Fact]
    public async Task RetryableArtistFailureAdvertisesItsExpiryAndOnlyRequeuesAfterIt()
    {
        var clock = new PhotoClock();
        using var caches = new LinerNotes.Infrastructure.Caching.DiscoveryMemoryCaches(new());
        var cache = new ArtistArtworkCache(clock, caches);
        cache.Request(Artist);
        Assert.Equal(Artist, await cache.TakeAsync(default));
        cache.Complete(Artist, null, retryable: true);
        Assert.Equal(TimeSpan.FromSeconds(30), cache.RetryDelay(Artist));
        Assert.Equal(RecordingArtworkState.Pending, cache.Request(Artist));
        clock.Now += TimeSpan.FromSeconds(31);
        Assert.Null(cache.RetryDelay(Artist));
        Assert.Equal(RecordingArtworkState.Pending, cache.Request(Artist));
        Assert.Equal(Artist, await cache.TakeAsync(default));
    }

    private sealed class PhotoClock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task CanonicalPhotoRequiresFreeLicenseAndOnlyDownloadsAllowlistedImage()
    {
        var calls = new List<string>();
        var photo = await AcquireAsync("CC BY-SA 4.0", calls);
        Assert.NotNull(photo);
        Assert.Equal("Photographer", photo.Credit);
        Assert.Equal("CC BY-SA 4.0", photo.License);
        Assert.Equal(3, calls.Count);
        Assert.StartsWith("https://upload.wikimedia.org/", calls[2]);
        Assert.True(photo.ExpiresAtUtc <= DateTimeOffset.UtcNow.AddMinutes(10));
    }

    [Theory]
    [InlineData("All rights reserved")]
    [InlineData("CC BY-NC 4.0")]
    public async Task UnapprovedLicenseNeverDownloadsImage(string license)
    {
        var calls = new List<string>();
        Assert.Null(await AcquireAsync(license, calls));
        Assert.Equal(2, calls.Count);
    }

    [Fact]
    public async Task MissingPhotoDoesNotAttemptImageRequest()
    {
        var calls = new List<string>();
        Assert.Null(await AcquireAsync("CC0", calls, noPhoto: true));
        Assert.Single(calls);
    }

    [Fact]
    public async Task RedirectAndPrivateMetadataAreNotRetainedOrFollowed()
    {
        foreach (var mode in new[] { "redirect", "private" })
        {
            var calls = new List<string>();
            Assert.Null(await AcquireAsync("CC0", calls, mode: mode));
            Assert.Single(calls);
        }
    }

    [Fact]
    public async Task PublicImageWithoutCacheDirectiveUsesBoundedHeuristicButPrivateImageIsRejected()
    {
        Assert.NotNull(await AcquireAsync("CC0", [], mode: "no-image-cache-header"));
        Assert.Null(await AcquireAsync("CC0", [], mode: "private-image"));
    }

    [Fact]
    public async Task CacheCoalescesDemandAndMissingPhotosAreNegativelyCached()
    {
        using var memory = new LinerNotes.Infrastructure.Caching.DiscoveryMemoryCaches(new());
        var cache = new ArtistArtworkCache(TimeProvider.System, memory);
        Assert.Equal(RecordingArtworkState.Pending, cache.Request(Artist));
        Assert.Equal(RecordingArtworkState.Pending, cache.Request(Artist));
        Assert.Equal(Artist, await cache.TakeAsync(default));
        cache.Complete(Artist, null);
        Assert.Equal(RecordingArtworkState.Unavailable, cache.Request(Artist));
        Assert.Null(cache.GetCached(Artist));
    }

    private static async Task<ArtistPhoto?> AcquireAsync(string license, List<string> calls, bool noPhoto = false, string mode = "")
    {
        var request = new ListenBrainzRequest(ListenBrainzRequestKind.ArtistArtworkMetadata, Artist.ToString());
        var metadata = Substitute.For<IListenBrainzApiClient>();
        var parsed = ListenBrainzResponse.Parse(request,
            System.Text.Encoding.UTF8.GetBytes("{\"relations\":[{\"type\":\"wikidata\",\"url\":{\"resource\":\"https://www.wikidata.org/wiki/Q123\"}}]}"),
            EvidenceOrigin.Live, DateTimeOffset.UtcNow) with { SharedCacheUntilUtc = DateTimeOffset.UtcNow.AddMinutes(10) };
        metadata.SendAsync(Arg.Any<ListenBrainzRequest>(), Arg.Any<CancellationToken>()).Returns(parsed);
        using var http = new HttpClient(new Handler(message =>
        {
            var url = message.RequestUri!.AbsoluteUri; calls.Add(url);
            var response = new HttpResponseMessage(mode == "redirect" ? HttpStatusCode.Found : HttpStatusCode.OK);
            response.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromHours(1), Private = mode == "private" };
            if (mode == "stale-metadata") response.Headers.Age = TimeSpan.FromHours(2);
            if (url.Contains("wikidata.org")) response.Content = new StringContent(noPhoto ? "{\"entities\":{\"Q123\":{\"claims\":{}}}}" :
                "{\"entities\":{\"Q123\":{\"claims\":{\"P18\":[{\"mainsnak\":{\"datavalue\":{\"value\":\"Example.jpg\"}}}]}}}}");
            else if (url.Contains("commons.wikimedia.org")) response.Content = new StringContent(
                "{\"query\":{\"pages\":{\"1\":{\"imageinfo\":[{\"thumburl\":\"https://upload.wikimedia.org/example.jpg\",\"descriptionurl\":\"https://commons.wikimedia.org/wiki/File:Example.jpg\",\"extmetadata\":{\"Artist\":{\"value\":\"<a>Photographer</a>\"},\"LicenseShortName\":{\"value\":\"" + license + "\"},\"LicenseUrl\":{\"value\":\"https://creativecommons.org/licenses/by-sa/4.0/\"}}}]}}}}");
            else {
                if (mode is "no-image-cache-header" or "headerless-image-with-age") response.Headers.CacheControl = null;
                if (mode == "headerless-image-with-age") response.Headers.Age = TimeSpan.FromDays(1);
                if (mode == "stale-image") response.Headers.Age = TimeSpan.FromHours(2);
                if (mode == "expires-image-with-age")
                {
                    response.Headers.CacheControl = null;
                    response.Headers.Date = DateTimeOffset.UtcNow.AddHours(-2);
                    response.Headers.Age = TimeSpan.FromHours(2);
                }
                if (mode == "private-image") response.Headers.CacheControl!.Private = true;
                response.Content = new ByteArrayContent([0xff, 0xd8, 0xff, 0x00]); response.Content.Headers.ContentType = new("image/jpeg");
                if (mode == "expires-image-with-age") response.Content.Headers.Expires = DateTimeOffset.UtcNow.AddMinutes(5); }
            return response;
        }));
        var clients = Substitute.For<IHttpClientFactory>(); clients.CreateClient("ArtistArtwork").Returns(http);
        var gate = Substitute.For<IMetaBrainzRequestGate>();
        var service = new MusicBrainzArtistArtwork(metadata, clients, gate, new ListenBrainzOptions { Mode = ListenBrainzClientMode.LiveOnly, LiveUseApproved = true }, TimeProvider.System);
        return await service.AcquireAsync(Artist, default);
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(send(request));
    }
}
