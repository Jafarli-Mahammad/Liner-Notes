using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LinerNotes.Presentation.Tests;

public sealed class Phase9SwaggerTests
{
    [Fact]
    public async Task Development_SwaggerRetainsBearerSecurityReferenceAfterUpgrade()
    {
        await using var app = new App();
        using var client = app.CreateClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var scheme = root.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", scheme.GetProperty("type").GetString());
        Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());
        Assert.True(root.GetProperty("security")[0].TryGetProperty("Bearer", out _));
    }

    [Fact]
    public async Task Production_DoesNotExposeSwaggerDocument()
    {
        await using var app = new App("Production");
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class App(string environment = "Development") : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=127.0.0.1;Database=unused;Username=unused");
            builder.UseSetting("Jwt:SecretKey", "phase9-fixture-signing-key-never-deploy-1234567890");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Database=unused;Username=unused",
                    ["Jwt:SecretKey"] = "phase9-fixture-signing-key-never-deploy-1234567890",
                    ["LastFm:Mode"] = "FixtureOnly",
                    ["Generation:Origin"] = "Synthetic"
                }));
        }
    }
}
