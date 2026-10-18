using System.Net;
using System.Security.Cryptography;
using System.Text;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace LinerNotes.Infrastructure.Tests;

public sealed class ListenBrainzTransportTests
{
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
            await Assert.ThrowsAsync<LinerNotes.Application.Common.Interfaces.Recommendation.GenerationStoppedException>(() => second.WaitAsync("ListenBrainz", default));
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
