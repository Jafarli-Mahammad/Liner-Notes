using System.IO.Compression;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LinerNotes.Presentation.Tests;

public sealed class StaticCompressionTests
{
    [Fact]
    public async Task PublicStaticGzipPreservesBytesAndRevalidation()
    {
        using var app = new TestApp();
        using var client = app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var plain = await client.GetAsync("/discovery.js");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/discovery.js");
        request.Headers.AcceptEncoding.ParseAdd("gzip");
        using var zipped = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, zipped.StatusCode);
        Assert.Contains("gzip", zipped.Content.Headers.ContentEncoding);
        Assert.Contains("Accept-Encoding", zipped.Headers.Vary);
        byte[] wire = await zipped.Content.ReadAsByteArrayAsync();
        byte[] expected = await plain.Content.ReadAsByteArrayAsync();
        await using var decoded = new GZipStream(new MemoryStream(wire), CompressionMode.Decompress);
        using var output = new MemoryStream(); await decoded.CopyToAsync(output);
        Assert.Equal(expected, output.ToArray());
        Assert.True(wire.Length < expected.Length / 2);
        using var conditional = new HttpRequestMessage(HttpMethod.Get, "/discovery.js");
        conditional.Headers.IfNoneMatch.Add(plain.Headers.ETag!);
        conditional.Headers.AcceptEncoding.ParseAdd("gzip");
        using var revalidated = await client.SendAsync(conditional);
        Assert.Equal(HttpStatusCode.NotModified, revalidated.StatusCode);
    }

    [Theory]
    [InlineData("/api/compression-boundary-probe", HttpStatusCode.OK)]
    [InlineData("/home-assets/missing.json", HttpStatusCode.NotFound)]
    public async Task PrivateJsonAndMissingAssetsNeverEnterCompression(string path, HttpStatusCode status)
    {
        using var app = new TestApp();
        using var client = app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.AcceptEncoding.ParseAdd("gzip, br");
        using var result = await client.SendAsync(request);
        Assert.Equal(status, result.StatusCode);
        Assert.Empty(result.Content.Headers.ContentEncoding);
        if (path.StartsWith("/api/"))
        {
            Assert.True(result.Headers.CacheControl!.NoStore);
            Assert.True((await result.Content.ReadAsByteArrayAsync()).Length > 5000);
        }
    }

    private sealed class TestApp : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Port=1;Database=unused;Username=unused",
                ["Jwt:SecretKey"] = "compression-test-signing-key-only-never-deploy-1234567890",
                ["LocalLive:Enabled"] = "false", ["LocalEmail:Enabled"] = "false",
                ["DiscoveryRuntime:RunJobs"] = "false", ["GuestDiscovery:Enabled"] = "false"
            };
            foreach (var setting in settings) builder.UseSetting(setting.Key, setting.Value);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
            builder.ConfigureServices(services => services.AddControllers().AddApplicationPart(typeof(CompressionBoundaryProbeController).Assembly));
        }
    }
}

[ApiController]
[Route("api/compression-boundary-probe")]
public sealed class CompressionBoundaryProbeController : ControllerBase
{
    [HttpGet]
    public object Read() => new { value = new string('x', 6000) };
}
