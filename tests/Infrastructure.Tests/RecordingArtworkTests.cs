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
    [Theory]
    [InlineData("request_timeout")]
    [InlineData("rate_limit")]
    [InlineData("transport_error")]
    [InlineData("upstream_unavailable")]
    public async Task TransientMetadataFailureRemainsPending(string reason)
    {
        using var cache = new RecordingArtworkCache();
        using var http = new HttpClient(new Handler(_ => throw new InvalidOperationException("No image without metadata")));
        var (service, metadata, _) = Create(cache, http);
        metadata.SendAsync(Arg.Any<ListenBrainzRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => ListenBrainzResponse.Gap(call.Arg<ListenBrainzRequest>(), EvidenceOrigin.Live, reason));
        await service.WarmAsync([Recording], default);
        Assert.Equal(RecordingArtworkState.Pending, service.Request(Recording));
        Assert.NotNull(service.RetryDelay(Recording));
    }

    [Fact]
    public async Task DifferentReleasesDoNotShareALongHeldHashStripe()
    {
        var slowRelease = Guid.NewGuid();
        var fastRelease = Guid.NewGuid();
        while ((uint)("release:" + slowRelease).GetHashCode() % 32 != (uint)("release:" + fastRelease).GetHashCode() % 32)
            fastRelease = Guid.NewGuid();
        var otherRecording = Guid.NewGuid();
        var slowStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fastStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cache = new RecordingArtworkCache();
        using var http = new HttpClient(new AsyncHandler(async (request, ct) =>
        {
            if (request.RequestUri!.AbsolutePath.Contains(slowRelease.ToString()))
            { slowStarted.TrySetResult(); await release.Task.WaitAsync(ct); }
            else fastStarted.TrySetResult();
            return Image();
        }));
        var (service, metadata, _) = Create(cache, http);
        metadata.SendAsync(Arg.Any<ListenBrainzRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
            ListenBrainzResponse.Parse(call.Arg<ListenBrainzRequest>(), System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
                new Dictionary<string, object>
                {
                    [Recording.ToString()] = new { release = new { mbid = slowRelease } },
                    [otherRecording.ToString()] = new { release = new { mbid = fastRelease } }
                }), EvidenceOrigin.Live, DateTimeOffset.UtcNow) with { SharedCacheUntilUtc = DateTimeOffset.UtcNow.AddHours(1) });
        var slow = service.WarmAsync([Recording], default);
        await slowStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var fast = service.WarmAsync([otherRecording], default);
        try { await fastStarted.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.False(release.Task.IsCompleted); }
        finally { release.TrySetResult(); await Task.WhenAll(slow, fast); }
    }

    private sealed class AsyncHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct);
    }

    [Fact]
    public async Task ConcurrentWarmupsShareTwoDownloadSlots()
    {
        using var cache = new RecordingArtworkCache();
        var ids = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).ToArray();
        var metadata = Substitute.For<IListenBrainzApiClient>();
        metadata.SendAsync(Arg.Any<ListenBrainzRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
            ListenBrainzResponse.Parse(call.Arg<ListenBrainzRequest>(), System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
                ids.ToDictionary(id => id.ToString("D"), id => new { release = new { mbid = id } })),
                EvidenceOrigin.Live, DateTimeOffset.UtcNow) with { SharedCacheUntilUtc = DateTimeOffset.UtcNow.AddHours(1) });
        var twoStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new BlockedDownloads(twoStarted, release);
        using var http = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("CoverArtArchive").Returns(_ => new HttpClient(new Forward(http)));
        var service = new RecordingArtwork(cache, metadata, factory, Substitute.For<IMetaBrainzRequestGate>(),
            new() { Mode = ListenBrainzClientMode.LiveOnly, LiveUseApproved = true }, TimeProvider.System);
        var pending = ids.Select(id => service.WarmAsync([id], default)).ToArray();
        try { await twoStarted.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { release.TrySetResult(); }
        await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, handler.Peak);
        Assert.All(ids, id => Assert.NotNull(service.GetCached(id)));
    }

    private sealed class BlockedDownloads(TaskCompletionSource started, TaskCompletionSource release) : HttpMessageHandler
    {
        private readonly object sync = new();
        private int active;
        public int Peak { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            lock (sync) { active++; Peak = Math.Max(Peak, active); if (active == 2) started.TrySetResult(); }
            try { await release.Task.WaitAsync(ct); await Task.Yield(); return Image(); }
            finally { lock (sync) active--; }
        }
    }

    [Fact]
    public async Task SustainedDemandDoesNotStarveAlreadyQueuedMaintenance()
    {
        using var cache = new RecordingArtworkCache();
        var maintenance = Guid.NewGuid(); cache.Enqueue([maintenance]);
        var observed = new List<Guid>();
        for (int round = 0; round < 4; round++)
        {
            cache.Enqueue(Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).ToArray(), priority: true);
            var batch = await cache.TakeAsync(default);
            observed.AddRange(batch); cache.Complete(batch);
        }
        Assert.Contains(maintenance, observed);
    }

    [Fact]
    public async Task VisibleDemandPromotesPreparedWorkWithoutRepeatingMetadata()
    {
        using var cache = new RecordingArtworkCache();
        var ids = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).ToArray();
        cache.Enqueue(ids);
        Assert.Equal(ids, await cache.TakeAsync(default));
        cache.Enqueue([ids[^1]], priority: true); // Demand arrives during metadata preparation.
        var plans = ids.Select(id => new ArtworkAcquisition(id, id, null, null, DateTimeOffset.UtcNow.AddMinutes(5))).ToArray();
        cache.QueuePrepared(plans);
        Assert.Equal(ids[^1], (await cache.TakePreparedAsync(default)).RecordingId);
        cache.Enqueue([ids[^2]], priority: true); // Demand also promotes an already prepared image.
        Assert.Equal(ids[^2], (await cache.TakePreparedAsync(default)).RecordingId);
        cache.QueuePrepared(plans); // An in-flight image must not acquire a second queue entry.
        var remaining = new List<Guid>();
        for (int i = 0; i < 6; i++) remaining.Add((await cache.TakePreparedAsync(default)).RecordingId);
        Assert.Equal(ids.Take(6), remaining);
        using var stop = new CancellationTokenSource(); stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.TakePreparedAsync(stop.Token));
    }

    [Fact]
    public async Task PreparedMaintenanceGetsALaneUnderSustainedVisibleDemand()
    {
        using var cache = new RecordingArtworkCache();
        var background = Guid.NewGuid(); cache.Enqueue([background]);
        await cache.TakeAsync(default);
        cache.QueuePrepared([new(background, background, null, null, DateTimeOffset.UtcNow.AddMinutes(5))]);
        var demand = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).ToArray();
        cache.Enqueue(demand, priority: true);
        var batch = await cache.TakeAsync(default);
        cache.QueuePrepared(batch.Select(id => new ArtworkAcquisition(id, id, null, null, DateTimeOffset.UtcNow.AddMinutes(5))).ToArray());
        var observed = new List<Guid>();
        for (int i = 0; i < 8; i++) observed.Add((await cache.TakePreparedAsync(default)).RecordingId);
        Assert.Equal(background, observed[7]);
        Assert.Equal(7, observed.Count(demand.Contains));
    }

    [Fact]
    public async Task InvalidImageIsUnavailableRatherThanContinuouslyPending()
    {
        using var cache = new RecordingArtworkCache();
        using var http = new HttpClient(new Handler(_ => new(HttpStatusCode.OK)
        {
            Content = new StringContent("invalid image", System.Text.Encoding.UTF8, "image/jpeg")
        }));
        var (service, _, _) = Create(cache, http);
        await service.WarmAsync([Recording], default);
        Assert.Equal(RecordingArtworkState.Unavailable, service.Request(Recording));
    }

    private static readonly Guid Recording = Guid.Parse("e97f805a-ab48-4c52-855e-07049142113d");
    private const string Release = "76df3287-6cda-33eb-8e9a-044b5e15ffdd";
    private const string Group = "48140466-cff6-3222-bd55-63c27e43190d";

    [Fact]
    public async Task RequestsPrioritizeVisibleCoversCoalesceDuplicatesAndNeverFetchOnRead()
    {
        using var cache = new RecordingArtworkCache();
        using var http = new HttpClient(new Handler(_ => throw new InvalidOperationException("No HTTP on a read")));
        var (service, metadata, _) = Create(cache, http);
        var old = Guid.NewGuid();
        cache.Enqueue([old, Recording]);
        Assert.Equal(RecordingArtworkState.Pending, service.Request(Recording));
        Assert.Equal(RecordingArtworkState.Pending, service.Request(Recording));
        var batch = await cache.TakeAsync(default);
        Assert.Equal(new[] { Recording, old }, batch);
        await metadata.DidNotReceive().SendAsync(Arg.Any<ListenBrainzRequest>(), Arg.Any<CancellationToken>());
        cache.Complete(batch);
        Assert.Equal(RecordingArtworkState.Unavailable, service.Request(Recording));
    }

    [Fact]
    public async Task QueueIsBoundedAndVisibleRequestsDisplaceMaintenance()
    {
        using var cache = new RecordingArtworkCache();
        var background = Enumerable.Range(0, 300).Select(_ => Guid.NewGuid()).ToArray();
        cache.Enqueue(background);
        var visible = Guid.NewGuid();
        cache.Enqueue([visible], priority: true);
        var all = new List<Guid>();
        for (int i = 0; i < 32; i++)
        {
            var batch = await cache.TakeAsync(default);
            all.AddRange(batch); cache.Complete(batch);
        }
        Assert.Equal(visible, all[0]);
        Assert.Equal(256, all.Distinct().Count());
        Assert.DoesNotContain(background[255], all);
        using var stop = new CancellationTokenSource(); stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.TakeAsync(stop.Token));
    }

    [Fact]
    public async Task UsesDistinctCaaReleaseBeforeGroupFallback()
    {
        using var cache = new RecordingArtworkCache();
        var paths = new List<string>();
        using var http = new HttpClient(new Handler(request =>
        {
            paths.Add(request.RequestUri!.AbsolutePath);
            return paths.Count == 1 ? new(HttpStatusCode.NotFound) : Image();
        }));
        var (service, metadata, _) = Create(cache, http);
        var caa = Guid.NewGuid();
        metadata.SendAsync(Arg.Any<ListenBrainzRequest>(), Arg.Any<CancellationToken>()).Returns(call => Task.FromResult(
            ListenBrainzResponse.Parse(call.Arg<ListenBrainzRequest>(), System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
                new Dictionary<string, object> { [Recording.ToString()] = new { release = new { mbid = Release, caa_release_mbid = caa, release_group_mbid = Group } } }),
                EvidenceOrigin.Live, DateTimeOffset.UtcNow) with { SharedCacheUntilUtc = DateTimeOffset.UtcNow.AddHours(1) }));
        await service.WarmAsync([Recording], default);
        Assert.Equal(new[] { $"/release/{Release}/front-500", $"/release/{caa}/front-500" }, paths);
        Assert.NotNull(service.GetCached(Recording));
    }

    [Fact]
    public async Task RestoresPersistedCoverWithoutUpstreamRequests()
    {
        using var cache = new RecordingArtworkCache();
        var metadata = Substitute.For<IListenBrainzApiClient>();
        var stored = Substitute.For<IStoredArtwork>();
        var cover = new RecordingCover([0xff, 0xd8, 0xff], "image/jpeg", DateTimeOffset.UtcNow.AddMinutes(5));
        stored.FindAsync(Recording, Arg.Any<CancellationToken>()).Returns(cover);
        var service = new RecordingArtwork(cache, metadata, Substitute.For<IHttpClientFactory>(), Substitute.For<IMetaBrainzRequestGate>(),
            new ListenBrainzOptions { Mode = ListenBrainzClientMode.LiveOnly, LiveUseApproved = true }, TimeProvider.System, stored);
        await service.WarmAsync([Recording], default, allowAcquisition: false);
        Assert.Same(cover, service.GetCached(Recording));
        await metadata.DidNotReceive().SendAsync(Arg.Any<ListenBrainzRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("no-store")]
    [InlineData("no-cache")]
    [InlineData("private")]
    [InlineData("public, max-age=0")]
    public async Task RestrictedImagesAreNeverReusedFromMemoryOrPersisted(string policy)
    {
        using var cache = new RecordingArtworkCache();
        using var http = new HttpClient(new Handler(_ =>
        {
            var response = Image(); response.Headers.CacheControl = System.Net.Http.Headers.CacheControlHeaderValue.Parse(policy); return response;
        }));
        var stored = Substitute.For<IStoredArtwork>();
        var (service, _, _) = Create(cache, http, stored);
        await service.WarmAsync([Recording], default);
        Assert.Null(service.GetCached(Recording));
        Assert.Equal(RecordingArtworkState.Unavailable, service.Request(Recording));
        await stored.DidNotReceive().SaveAsync(Arg.Any<Guid>(), Arg.Any<RecordingCover>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UncacheableRecordingMappingsDoNotEnterPersistentImageStorage()
    {
        using var cache = new RecordingArtworkCache();
        using var http = new HttpClient(new Handler(_ => throw new InvalidOperationException("Uncacheable mappings must not trigger image requests")));
        var stored = Substitute.For<IStoredArtwork>();
        var (service, metadata, _) = Create(cache, http, stored);
        metadata.SendAsync(Arg.Any<ListenBrainzRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
            ListenBrainzResponse.Parse(call.Arg<ListenBrainzRequest>(), System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
                new Dictionary<string, object> { [Recording.ToString()] = new { release = new { mbid = Release } } }),
                EvidenceOrigin.Live, DateTimeOffset.UtcNow));
        await service.WarmAsync([Recording], default);
        Assert.Null(service.GetCached(Recording));
        await stored.DidNotReceive().SaveAsync(Arg.Any<Guid>(), Arg.Any<RecordingCover>(), Arg.Any<CancellationToken>());
    }

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
        Assert.Equal(RecordingArtworkState.Pending, service.Request(Recording));
        Assert.InRange(service.RetryDelay(Recording)!.Value.TotalSeconds, 55, 60);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.WarmAsync([Guid.NewGuid()], cancelled.Token));
    }

    private static (RecordingArtwork Service, IListenBrainzApiClient Metadata, IMetaBrainzRequestGate Gate)
        Create(RecordingArtworkCache cache, HttpClient http, IStoredArtwork? stored = null)
    {
        var metadata = Substitute.For<IListenBrainzApiClient>();
        metadata.SendAsync(Arg.Any<ListenBrainzRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
            ListenBrainzResponse.Parse(call.Arg<ListenBrainzRequest>(), System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
                new Dictionary<string, object> { [Recording.ToString()] = new { release = new { mbid = Release, release_group_mbid = Group } } }),
                EvidenceOrigin.Live, DateTimeOffset.UtcNow) with { SharedCacheUntilUtc = DateTimeOffset.UtcNow.AddHours(1) });
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("CoverArtArchive").Returns(_ => new HttpClient(new Forward(http)));
        var gate = Substitute.For<IMetaBrainzRequestGate>();
        return (new(cache, metadata, factory, gate, new() { Mode = ListenBrainzClientMode.LiveOnly, LiveUseApproved = true }, TimeProvider.System, stored), metadata, gate);
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
