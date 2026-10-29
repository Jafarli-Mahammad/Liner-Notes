using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LinerNotes.Infrastructure.Tests;

public sealed class MetadataCompressionTests
{
    [Theory]
    [InlineData("gzip", false)]
    [InlineData("br", false)]
    [InlineData("deflate", false)]
    [InlineData("gzip", true)]
    [InlineData("br", true)]
    [InlineData("deflate", true)]
    public async Task ProductionHandlerNegotiatesCompressionAndEnforcesDecodedLimit(string encoding, bool oversized)
    {
        byte[] body = Encoding.UTF8.GetBytes("{\"padding\":\"" + new string('x', oversized ? 1_000_001 : 100_000) + "\"}");
        using var compressed = new MemoryStream();
        using (Stream compressor = encoding switch
        {
            "gzip" => new GZipStream(compressed, CompressionLevel.Fastest, true),
            "br" => new BrotliStream(compressed, CompressionLevel.Fastest, true),
            _ => new DeflateStream(compressed, CompressionLevel.Fastest, true)
        }) compressor.Write(body);
        byte[] wire = compressed.ToArray();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        async Task<string> ServeAsync()
        {
            using var connection = await listener.AcceptTcpClientAsync(deadline.Token);
            await using var stream = connection.GetStream();
            using var reader = new StreamReader(stream, leaveOpen: true);
            string headers = "";
            while (await reader.ReadLineAsync(deadline.Token) is { Length: > 0 } line) headers += line + "\n";
            byte[] header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Encoding: {encoding}\r\nCache-Control: public, max-age=600\r\nContent-Length: {wire.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(header, deadline.Token);
            await stream.WriteAsync(wire, deadline.Token);
            return headers;
        }
        var server = ServeAsync();
        using var services = new ServiceCollection().AddLogging()
            .AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ListenBrainz:Mode"] = "LiveOnly", ["ListenBrainz:LiveUseApproved"] = "true"
            }).Build())
            .AddSingleton<IMetaBrainzRequestGate>(new NoGate())
            .AddHttpClient<ListenBrainzApiClient>()
            .AddHttpMessageHandler(() => new LoopbackEndpoint(new Uri($"http://127.0.0.1:{endpoint.Port}/")))
            .Services.BuildServiceProvider();
        var client = services.GetRequiredService<ListenBrainzApiClient>();
        var request = new ListenBrainzRequest(ListenBrainzRequestKind.TagRadio, "jazz");
        var result = await client.SendAsync(request, deadline.Token);
        var received = await server;
        Assert.Contains("Accept-Encoding:", received, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(encoding, received);
        if (oversized) Assert.Equal("response_size_limit", Assert.Single(result.Gaps).Reason);
        else
        {
            Assert.Empty(result.Gaps);
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(body)), result.Reference.ResponseSha256);
            Assert.Same(result, await client.SendAsync(request, deadline.Token));
            Assert.True(wire.Length < body.Length / 10);
        }
    }

    private sealed class LoopbackEndpoint(Uri endpoint) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            request.RequestUri = new Uri(endpoint, request.RequestUri!.PathAndQuery);
            return base.SendAsync(request, ct);
        }
    }

    private sealed class NoGate : IMetaBrainzRequestGate
    {
        public Task WaitAsync(string provider, CancellationToken ct) => Task.CompletedTask;
        public Task DeferAsync(string provider, TimeSpan delay, CancellationToken ct) => Task.CompletedTask;
    }
}
