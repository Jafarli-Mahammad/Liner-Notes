using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LinerNotes.Presentation.Tests;

/// <summary>Production HTTP/security fixtures; actual protocol startup is covered by PostgreSQL process tests.</summary>
internal static class ProductionRoutingFixture
{
    public static void Configure(IWebHostBuilder builder)
    {
        var root = Path.Combine(Path.GetTempPath(), "liner-production-routing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var settings = ProductionConfigurationTests.Valid();
        settings["AllowedHosts"] = "localhost";
        settings["GuestDiscovery:PublicOrigin"] = "https://localhost";
        settings["DataProtection:KeyPath"] = root;
        settings["ForwardedHeaders:KnownProxies:0"] = "127.0.0.1";
        settings["ForwardedHeaders:KnownProxies:1"] = "::1";
        foreach (var pair in settings) builder.UseSetting(pair.Key, pair.Value);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
        builder.ConfigureServices(services =>
        {
            // These routing fixtures have no initialized storage/database. Never disable this in a release image.
            foreach (var registration in services.Where(s => s.ServiceType == typeof(IHostedService) &&
                s.ImplementationType?.Name is "StorageProtocolStartup" or "StorageRecoveryService" or "OperationalHealth").ToArray())
                services.Remove(registration);
            services.AddSingleton<IHostedService>(_ => new StateLifetime(root));
        });
    }

    private sealed class StateLifetime(string root) : IHostedService, IDisposable
    {
        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
        public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
}
