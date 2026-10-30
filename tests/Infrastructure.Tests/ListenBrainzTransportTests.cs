using System.Net;
using System.Security.Cryptography;
using System.Text;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;
using Microsoft.Extensions.Caching.Memory;
using Xunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LinerNotes.Infrastructure.Tests;

public sealed class ListenBrainzTransportTests
{
    [Fact]
    public async Task RegisteredClientAllowsSlowRadioResponseWithoutRestartingAcquisition()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Generation:Provider"] = "listenbrainz", ["Generation:Origin"] = "Synthetic",
            ["ListenBrainz:Mode"] = "LiveOnly", ["ListenBrainz:LiveUseApproved"] = "true"
        }).Build();
        var registrations = new ServiceCollection().AddLogging().AddInfrastructure(configuration);
        registrations.AddSingleton<IMetaBrainzRequestGate>(new Gate());
        bool completed = false;
        int dispatches = 0;
        registrations.AddHttpClient<ListenBrainzApiClient>().ConfigurePrimaryHttpMessageHandler(() => new AsyncHandler(async (_, ct) =>
        {
            Interlocked.Increment(ref dispatches);
            await Task.Delay(TimeSpan.FromSeconds(16), ct);
            completed = true;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") };
        }));
        await using var services = registrations.BuildServiceProvider();
        var response = await services.GetRequiredService<ListenBrainzApiClient>()
            .SendAsync(new(ListenBrainzRequestKind.TagRadio, "slow-radio"));
        Assert.Empty(response.Gaps);
        Assert.True(completed);
        Assert.Equal(1, dispatches);
    }

    [Theory]
    [InlineData("public, max-age=60", 1)]
    [InlineData("no-store", 8)]
    [InlineData("no-cache", 8)]
    [InlineData("private", 8)]
    public async Task OverlappingRequestsShareOnlyRetainableResponses(string policy, int expectedRequests)
    {
        using var caches = new LinerNotes.Infrastructure.Caching.DiscoveryMemoryCaches(new());
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int requests = 0;
        using var http = new HttpClient(new AsyncHandler(async (_, ct) =>
        {
            Interlocked.Increment(ref requests); started.TrySetResult(); await release.Task.WaitAsync(ct);
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") };
            response.Headers.CacheControl = System.Net.Http.Headers.CacheControlHeaderValue.Parse(policy);
            return response;
        }));
        var options = new ListenBrainzOptions { Mode = ListenBrainzClientMode.LiveOnly, LiveUseApproved = true, MaximumRequests = 1 };
        var clients = Enumerable.Range(0, 8).Select(_ => new ListenBrainzApiClient(http, options, new Gate(), caches.Metadata, TimeProvider.System, caches)).ToArray();
        var request = new ListenBrainzRequest(ListenBrainzRequestKind.TagRadio, "coalesced-fixture");
        var pending = clients.Select(client => client.SendAsync(request)).ToArray();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        release.TrySetResult();
        var responses = await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(expectedRequests, requests);
        Assert.All(responses, response => Assert.Empty(response.Gaps));
        if (expectedRequests == 1) Assert.All(responses, response => Assert.Same(responses[0], response));
    }

    [Fact]
    public async Task CancelledOwnerDoesNotCancelIndependentWaiterOrConsumeItsBudget()
    {
        using var caches = new LinerNotes.Infrastructure.Caching.DiscoveryMemoryCaches(new());
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int requests = 0;
        using var http = new HttpClient(new AsyncHandler(async (_, ct) =>
        {
            if (Interlocked.Increment(ref requests) == 1)
            { started.TrySetResult(); await Task.Delay(Timeout.Infinite, ct); }
            return new(HttpStatusCode.OK) { Content = new StringContent("[]") };
        }));
        var options = new ListenBrainzOptions { Mode = ListenBrainzClientMode.LiveOnly, LiveUseApproved = true, MaximumRequests = 1 };
        var owner = new ListenBrainzApiClient(http, options, new Gate(), caches.Metadata, TimeProvider.System, caches);
        var waiter = new ListenBrainzApiClient(http, options, new Gate(), caches.Metadata, TimeProvider.System, caches);
        var request = new ListenBrainzRequest(ListenBrainzRequestKind.TagRadio, "cancelled-owner-fixture");
        using var stop = new CancellationTokenSource();
        var first = owner.SendAsync(request, stop.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = waiter.SendAsync(request); stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.Empty((await second.WaitAsync(TimeSpan.FromSeconds(5))).Gaps);
        Assert.Equal(2, requests);
    }

    private sealed class AsyncHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => respond(request, ct);
    }

    [Fact]
    public async Task CacheFilledDuringPacingAvoidsDispatchAndDoesNotConsumeRequestBudget()
    {
        int requests = 0;
        using var http = new HttpClient(new Handler(_ =>
        {
            requests++;
            return new(HttpStatusCode.OK) { Content = new StringContent("[]") };
        }));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var paused = new PausedGate();
        var options = new ListenBrainzOptions { Mode = ListenBrainzClientMode.LiveOnly, LiveUseApproved = true, MaximumRequests = 1 };
        var waiting = new ListenBrainzApiClient(http, options, paused, cache, TimeProvider.System);
        var other = new ListenBrainzApiClient(http, options, new Gate(), cache, TimeProvider.System);
        var request = new ListenBrainzRequest(ListenBrainzRequestKind.TagRadio, "jazz");
        var pending = waiting.SendAsync(request);
        await paused.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var populated = await other.SendAsync(request);
        paused.Release.TrySetResult();
        var reused = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Same(populated, reused);
        Assert.Equal(1, requests);
        await waiting.SendAsync(new(ListenBrainzRequestKind.TagRadio, "rock"));
        Assert.Equal(2, requests);
        await Assert.ThrowsAsync<LinerNotes.Application.Common.Interfaces.Recommendation.GenerationStoppedException>(() =>
            waiting.SendAsync(new(ListenBrainzRequestKind.TagRadio, "ambient")));
        Assert.Equal(2, requests);
    }

    private sealed class PausedGate : IMetaBrainzRequestGate
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task WaitAsync(string provider, CancellationToken cancellationToken)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }
        public Task DeferAsync(string provider, TimeSpan delay, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Theory]
    [InlineData("public, max-age=0, s-maxage=120", 1)]
    [InlineData("public, max-age=0", 2)]
    [InlineData("no-store, max-age=0, s-maxage=120", 2)]
    [InlineData("private, max-age=0, s-maxage=120", 2)]
    [InlineData("no-cache, max-age=0, s-maxage=120", 2)]
    public async Task SharedFreshness_UsesSharedLifetimeAndHonorsRestrictions(string policy, int expectedRequests)
    {
        int requests = 0;
        using var http = new HttpClient(new Handler(_ =>
        {
            requests++;
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") };
            response.Headers.CacheControl = System.Net.Http.Headers.CacheControlHeaderValue.Parse(policy);
            return response;
        }));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new ListenBrainzApiClient(http, new() { Mode = ListenBrainzClientMode.LiveOnly,
            LiveUseApproved = true }, new Gate(), cache, TimeProvider.System);
        var request = new ListenBrainzRequest(ListenBrainzRequestKind.TagRadio, "rock", 1);
        var first = await client.SendAsync(request);
        await client.SendAsync(request);
        Assert.Equal(expectedRequests, requests);
        if (expectedRequests == 1)
            Assert.InRange(first.SharedCacheUntilUtc!.Value, DateTimeOffset.UtcNow.AddSeconds(110), DateTimeOffset.UtcNow.AddSeconds(121));
        else Assert.Null(first.SharedCacheUntilUtc);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "permission_denied")]
    [InlineData(HttpStatusCode.Forbidden, "permission_denied")]
    [InlineData(HttpStatusCode.NotFound, "not_found")]
    [InlineData(HttpStatusCode.InternalServerError, "upstream_unavailable")]
    [InlineData(HttpStatusCode.BadRequest, "http_error")]
    public async Task HttpFailuresDistinguishPermissionAndRetryableAvailability(HttpStatusCode status, string expected)
    {
        using var http = new HttpClient(new Handler(_ => new(status) { Content = new StringContent("{}") }));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new ListenBrainzApiClient(http, new() { Mode = ListenBrainzClientMode.LiveOnly, LiveUseApproved = true }, new Gate(), cache, TimeProvider.System);
        var response = await client.SendAsync(new(ListenBrainzRequestKind.TagRadio, "jazz", 5));
        Assert.Equal(expected, Assert.Single(response.Gaps).Reason);
    }
    [Fact]
    public async Task TokenIsSentOnlyToListenBrainzAndCacheAvoidsFurtherCalls()
    {
        var requests = new List<(Uri Uri, string? Scheme, string? Token)>();
        using var http = new HttpClient(new Handler(request =>
        {
            requests.Add((request.RequestUri!, request.Headers.Authorization?.Scheme, request.Headers.Authorization?.Parameter));
            return new(HttpStatusCode.OK) { Content = new StringContent("[]") };
        }));
        using var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1024 * 1024 });
        var gate = new Gate();
        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
        var client = new ListenBrainzApiClient(http, new() { Mode = ListenBrainzClientMode.LiveOnly, LiveUseApproved = true, Token = token }, gate, cache, TimeProvider.System);
        var artist = new ListenBrainzRequest(ListenBrainzRequestKind.ArtistSearch, "Jakuzi");
        var tags = new ListenBrainzRequest(ListenBrainzRequestKind.TagRadio, "synth pop");
        var first = await client.SendAsync(artist); await client.SendAsync(tags); await client.SendAsync(tags);
        Assert.NotNull(first.SharedCacheUntilUtc);
        Assert.Equal(2, requests.Count);
        Assert.Equal("musicbrainz.org", requests[0].Uri.Host);
        Assert.Null(requests[0].Token);
        Assert.Equal("api.listenbrainz.org", requests[1].Uri.Host);
        Assert.Equal("Token", requests[1].Scheme);
        Assert.Equal(token, requests[1].Token);
        Assert.DoesNotContain(token, requests[1].Uri.ToString());
        Assert.Contains("pop_begin=0&pop_end=100", requests[1].Uri.Query);
        Assert.Equal(2, gate.Calls);
    }

    [Fact]
    public async Task UsageGatePreventsHttpEvenWithToken()
    {
        using var http = new HttpClient(new Handler(_ => throw new InvalidOperationException("HTTP forbidden")));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var gate = new Gate();
        var client = new ListenBrainzApiClient(http, new() { Mode = ListenBrainzClientMode.LiveOnly }, gate, cache, TimeProvider.System);
        var response = await client.SendAsync(new(ListenBrainzRequestKind.TagRadio, "rock"));
        Assert.Equal("listenbrainz_live_use_unapproved", Assert.Single(response.Gaps).Reason);
        Assert.Equal(0, gate.Calls);
    }

    [Fact]
    public async Task RateLimitDefersSharedBudgetWithoutRetryOrFixtureFallback()
    {
        int calls = 0;
        using var http = new HttpClient(new Handler(_ =>
        {
            calls++;
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("{}") };
            response.Headers.Add("X-RateLimit-Remaining", "0");
            response.Headers.Add("X-RateLimit-Reset-In", "120");
            return response;
        }));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var gate = new Gate();
        var client = new ListenBrainzApiClient(http, new() { Mode = ListenBrainzClientMode.LiveOnly, LiveUseApproved = true }, gate, cache, TimeProvider.System);
        var response = await client.SendAsync(new(ListenBrainzRequestKind.TagRadio, "rock"));
        Assert.Equal(1, calls); Assert.Equal(TimeSpan.FromSeconds(120), gate.Deferred);
        Assert.Null(response.Data); Assert.Equal(EvidenceOrigin.Live, response.Reference.Origin);
        Assert.Equal("rate_limit", Assert.Single(response.Gaps).Reason);
    }

    [Theory]
    [InlineData("{broken", "invalid_json")]
    [InlineData("oversized", "response_size_limit")]
    public async Task InvalidBodiesBecomeExplicitGaps(string body, string reason)
    {
        using var http = new HttpClient(new Handler(_ => new(HttpStatusCode.OK)
            { Content = new StringContent(body == "oversized" ? new string('x', ListenBrainzResponse.MaximumBytes + 1) : body) }));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new ListenBrainzApiClient(http, new() { Mode = ListenBrainzClientMode.LiveOnly, LiveUseApproved = true }, new Gate(), cache, TimeProvider.System);
        Assert.Equal(reason, Assert.Single((await client.SendAsync(new(ListenBrainzRequestKind.TagRadio, "rock"))).Gaps).Reason);
    }

    [Fact]
    public async Task ReplayCopiesBytesAndRejectsMissingRequestsAndHashTampering()
    {
        var request = new ListenBrainzRequest(ListenBrainzRequestKind.TagRadio, "rock");
        byte[] body = Encoding.UTF8.GetBytes("[]");
        string hash = Convert.ToHexStringLower(SHA256.HashData(body));
        var entry = new RecordedListenBrainzResponse(request, body, hash, DateTimeOffset.UnixEpoch);
        body[0] = 0;
        var client = new RecordedListenBrainzApiClient([entry]);
        var response = await client.SendAsync(request);
        Assert.Equal(EvidenceOrigin.Recorded, response.Reference.Origin);
        Assert.Equal(hash, response.Reference.ResponseSha256); Assert.Empty(response.Gaps);
        Assert.Equal("recording_request_not_available", Assert.Single((await client.SendAsync(request)).Gaps).Reason);
        Assert.Throws<ArgumentException>(() => new RecordedListenBrainzResponse(request, body, hash, DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public async Task CancellationIsNotConvertedIntoTransportGap()
    {
        using var http = new HttpClient(new Handler(_ => throw new HttpRequestException("sensitive exception")));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new ListenBrainzApiClient(http, new() { Mode = ListenBrainzClientMode.LiveOnly, LiveUseApproved = true }, new Gate(), cache, TimeProvider.System);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.SendAsync(new(ListenBrainzRequestKind.TagRadio, "rock"), cancellation.Token));
        var response = await client.SendAsync(new(ListenBrainzRequestKind.TagRadio, "rock"));
        Assert.Equal("transport_error", Assert.Single(response.Gaps).Reason);
    }

    [Fact]
    public async Task IndependentGateInstancesSharePersistedPacingAndBackoff()
    {
        string root = Path.Combine(Path.GetTempPath(), "liner-lb-gate-" + Guid.NewGuid().ToString("N"));
        try
        {
            var options = new ListenBrainzOptions { RateLimitDirectory = root };
            var first = new MetaBrainzRequestGate(options, TimeProvider.System);
            var second = new MetaBrainzRequestGate(options, TimeProvider.System);
            await first.WaitAsync("ListenBrainz", default);
            var started = System.Diagnostics.Stopwatch.StartNew();
            await second.WaitAsync("ListenBrainz", default);
            Assert.True(started.Elapsed >= TimeSpan.FromSeconds(1));
            await first.DeferAsync("ListenBrainz", TimeSpan.FromMinutes(2), default);
            var deferred = await Assert.ThrowsAsync<LinerNotes.Application.Common.Interfaces.Discovery.DiscoverySourceException>(() => second.WaitAsync("ListenBrainz", default));
            Assert.True(deferred.Retryable);
            Assert.InRange(deferred.RetryAfter!.Value.TotalSeconds, 115, 120);
            await second.WaitAsync("MusicBrainz", default); // Separate upstream budget.
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RequestBudgetStopsBeforeAnotherNetworkCall()
    {
        int calls = 0;
        using var http = new HttpClient(new Handler(_ => { calls++; return new(HttpStatusCode.OK) { Content = new StringContent("[]") }; }));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new ListenBrainzApiClient(http, new() { Mode = ListenBrainzClientMode.LiveOnly, LiveUseApproved = true, MaximumRequests = 1 }, new Gate(), cache, TimeProvider.System);
        await client.SendAsync(new(ListenBrainzRequestKind.TagRadio, "rock"));
        var error = await Assert.ThrowsAsync<LinerNotes.Application.Common.Interfaces.Recommendation.GenerationStoppedException>(() =>
            client.SendAsync(new(ListenBrainzRequestKind.TagRadio, "electronic")));
        Assert.Equal("upstream_request_limit", error.Message); Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("no-store")]
    [InlineData("no-cache")]
    [InlineData("private")]
    [InlineData("expired")]
    public async Task RestrictedResponsesNeverGrantSharedCatalogRetention(string policy)
    {
        int requests = 0;
        using var http = new HttpClient(new Handler(_ =>
        {
            requests++;
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") };
            if (policy == "expired") response.Content.Headers.Expires = DateTimeOffset.UnixEpoch;
            else response.Headers.CacheControl = System.Net.Http.Headers.CacheControlHeaderValue.Parse(policy);
            return response;
        }));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new ListenBrainzApiClient(http, new() { Mode = ListenBrainzClientMode.LiveOnly, LiveUseApproved = true }, new Gate(), cache, TimeProvider.System);
        var request = new ListenBrainzRequest(ListenBrainzRequestKind.ArtistSearch, "Synthetic cache-policy fixture");
        Assert.Null((await client.SendAsync(request)).SharedCacheUntilUtc);
        Assert.Null((await client.SendAsync(request)).SharedCacheUntilUtc);
        Assert.Equal(2, requests);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
    private sealed class Gate : IMetaBrainzRequestGate
    {
        public int Calls { get; private set; }
        public TimeSpan Deferred { get; private set; }
        public Task WaitAsync(string provider, CancellationToken cancellationToken) { Calls++; return Task.CompletedTask; }
        public Task DeferAsync(string provider, TimeSpan delay, CancellationToken cancellationToken) { Deferred = delay; return Task.CompletedTask; }
    }
}
