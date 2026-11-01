using LinerNotes.Presentation.Production;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace LinerNotes.Presentation.Tests;

public sealed class ProductionConfigurationTests
{
    [Fact]
    public void ProductionKeysDecryptAfterServiceProviderRestart()
    {
        var directory = Directory.CreateTempSubdirectory("liner-production-keys-");
        try
        {
            var values = Valid();
            values["Environment"] = "Production";
            values["DataProtection:KeyPath"] = directory.FullName;
            values["Jwt:SecretKey"] = "production-key-persistence-fixture-only-not-a-real-secret";
            var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
            string protectedValue;
            using (var first = new ServiceCollection().AddLogging().AddPresentation(config).BuildServiceProvider())
                protectedValue = first.GetRequiredService<IDataProtectionProvider>().CreateProtector("restart-fixture").Protect("fixture-payload");
            Assert.NotEmpty(directory.GetFiles("key-*.xml"));
            using var second = new ServiceCollection().AddLogging().AddPresentation(config).BuildServiceProvider();
            Assert.Equal("fixture-payload", second.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("restart-fixture").Unprotect(protectedValue));
        }
        finally { directory.Delete(recursive: true); }
    }

    internal static Dictionary<string, string?> Valid() => new()
    {
        ["GuestDiscovery:PublicOrigin"] = "https://music.example.test",
        ["AllowedHosts"] = "music.example.test",
        ["ForwardedHeaders:KnownProxies:0"] = "172.30.0.2",
        ["Generation:Provider"] = "listenbrainz",
        ["Generation:Origin"] = "Live",
        ["Generation:LiveEnabled"] = "true",
        ["ListenBrainz:Mode"] = "LiveOnly",
        ["ListenBrainz:LiveUseApproved"] = "true",
        ["DiscoveryRuntime:EnforceStorageAdmission"] = "true",
        ["GenerationStorage:InventoryPath"] = "/app/state/inventory.json",
        ["GenerationStorage:LeasePath"] = "/app/state/lease.lock",
        ["DataProtection:KeyPath"] = "/app/keys",
    };

    [Theory]
    [InlineData("AllowedHosts", "*")]
    [InlineData("AllowedHosts", "other.example.test")]
    [InlineData("GuestDiscovery:PublicOrigin", "http://music.example.test")]
    [InlineData("GuestDiscovery:PublicOrigin", "https://music.example.test/path")]
    [InlineData("GuestDiscovery:PublicOrigin", "https://user@music.example.test")]
    [InlineData("ForwardedHeaders:KnownProxies:0", "not-an-address")]
    [InlineData("LocalLive:Enabled", "true")]
    [InlineData("LocalEmail:Enabled", "true")]
    [InlineData("FORWARDEDHEADERS_ENABLED", "true")]
    [InlineData("Generation:Origin", "Synthetic")]
    [InlineData("Generation:LiveEnabled", "false")]
    [InlineData("ListenBrainz:Mode", "FixtureOnly")]
    [InlineData("ListenBrainz:LiveUseApproved", "false")]
    [InlineData("DiscoveryRuntime:EnforceStorageAdmission", "false")]
    [InlineData("GenerationStorage:InventoryPath", "relative.json")]
    [InlineData("DataProtection:KeyPath", "relative-keys")]
    public void UnsafeProductionSettingsAreRejected(string key, string? value)
    {
        var values = Valid(); values[key] = value;
        var error = Assert.Throws<InvalidOperationException>(() => ProductionConfiguration.Validate(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build()));
        Assert.StartsWith("production_", error.Message);
        Assert.DoesNotContain(value ?? "unused", error.Message);
    }

    [Fact]
    public void PausedConsumersDoNotDisableAdmissionOrRequireUpstreamAccess()
    {
        var values = Valid(); values["DiscoveryRuntime:RunJobs"] = "false";
        ProductionConfiguration.Validate(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
    }
}
