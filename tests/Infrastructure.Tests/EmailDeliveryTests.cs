using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Infrastructure.Email;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace LinerNotes.Infrastructure.Tests;

public sealed class EmailDeliveryTests
{
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeSeconds(1731705121); }
    [Fact]
    public void PingSignatureRequiresExactBodyAndFreshTimestamp()
    {
        var key = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EmailDelivery:WebhookSecret"] = "whsec_" + Convert.ToBase64String(key)
        }).Build();
        var verifier = new ResendEventVerifier(config, new Clock());
        var body = Encoding.UTF8.GetBytes("{\"type\":\"ping\",\"data\":{\"success\":true}}");
        const string id = "fixture-message";
        const string timestamp = "1731705121";
        var signed = Encoding.UTF8.GetBytes($"{id}.{timestamp}.{Encoding.UTF8.GetString(body)}");
        var signature = "v1," + Convert.ToBase64String(HMACSHA256.HashData(key, signed));
        Assert.Equal("ignored", verifier.Verify(body, id, timestamp, signature)!.Outcome);
        Assert.Null(verifier.Verify(Encoding.UTF8.GetBytes("tampered"), id, timestamp, signature));
        Assert.Null(verifier.Verify(body, id, "1731704000", signature));
    }
    [Fact]
    public void SignedDeliveryEventAndTamperingAreDistinguished()
    {
        var key = RandomNumberGenerator.GetBytes(32); var clock = new Clock();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["EmailDelivery:WebhookSecret"] = "whsec_" + Convert.ToBase64String(key) }).Build();
        var verifier = new ResendEventVerifier(config, clock);
        var body = "{\"type\":\"email.delivered\",\"data\":{\"email_id\":\"fixture\"}}";
        var signature = "v1," + Convert.ToBase64String(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes("evt." + clock.GetUtcNow().ToUnixTimeSeconds() + "." + body)));
        Assert.Equal("delivered", verifier.Verify(Encoding.UTF8.GetBytes(body), "evt", "1731705121", signature)!.Outcome);
        Assert.Null(verifier.Verify(Encoding.UTF8.GetBytes(body + " "), "evt", "1731705121", signature));
        Assert.Null(verifier.Verify(Encoding.UTF8.GetBytes(body), "evt", "1731705121", "v1,broken"));
    }
    private sealed class Handler : HttpMessageHandler
    {
        public List<(string Key, string Body)> Requests { get; } = [];
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("api.resend.com", request.RequestUri!.Host); Assert.Equal("https", request.RequestUri.Scheme);
            if (request.Method == HttpMethod.Get)
            {
                Assert.Equal("/emails/37e4414c-5e25-4dbc-a071-43552a4bd53b", request.RequestUri.AbsolutePath);
                Assert.Null(request.Content);
                return new(Status) { Content = new StringContent("{\"last_event\":\"delivered\"}") };
            }
            Requests.Add((request.Headers.GetValues("Idempotency-Key").Single(), await request.Content!.ReadAsStringAsync(ct)));
            return new(Status) { Content = new StringContent("{\"id\":\"fixture-message\"}") };
        }
    }
    [Fact]
    public async Task ReconciliationReadsKnownMessageWithoutSendingAgain()
    {
        var handler = new Handler();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["EmailDelivery:ApiKey"] = "fixture-only" }).Build();
        var transport = new ResendEmailTransport(new HttpClient(handler), config, new());
        Assert.Equal("delivered", (await transport.ReconcileAsync("37e4414c-5e25-4dbc-a071-43552a4bd53b", default)).Status);
        Assert.Empty(handler.Requests);
        Assert.Equal("rejected", (await transport.ReconcileAsync("../../other", default)).Status);
    }
    [Fact]
    public async Task RetryUsesStableBodyAndKeyAndAcceptanceDoesNotClaimDelivery()
    {
        var handler = new Handler(); var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["EmailDelivery:ApiKey"] = "fixture-only" }).Build();
        var transport = new ResendEmailTransport(new HttpClient(handler), config, new() { Enabled = true, PublicOrigin = "https://music.example.test", FromAddress = "sender@example.test" });
        var payload = new EmailPayload("listener@example.test", "Fixture", "stored text", "<p>stored text</p>", new Dictionary<string, string>(), []);
        Assert.Equal("accepted", (await transport.SendAsync(payload, "stable-key", default)).Status);
        await transport.SendAsync(payload, "stable-key", default);
        Assert.Equal(handler.Requests[0], handler.Requests[1]);
        handler.Status = HttpStatusCode.TooManyRequests; Assert.Equal("retry", (await transport.SendAsync(payload, "stable-key", default)).Status);
        handler.Status = HttpStatusCode.BadRequest; Assert.Equal("rejected", (await transport.SendAsync(payload, "stable-key", default)).Status);
    }
}
