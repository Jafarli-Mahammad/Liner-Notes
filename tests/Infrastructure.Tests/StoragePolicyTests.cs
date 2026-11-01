using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Infrastructure.Storage;
using Xunit;

namespace LinerNotes.Infrastructure.Tests;

public sealed class StoragePolicyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MaxValue)]
    public void InvalidCapacityFailsClosed(long capacity)
    {
        var options = Options(); options.StopBytes = capacity;
        var error = Assert.Throws<GenerationStoppedException>(() => options.PolicyFingerprint());
        Assert.Equal("storage_capacity_invalid", error.Message);
    }

    [Fact]
    public void CapacityAndCoordinationPathsArePartOfWriterAgreement()
    {
        var original = Options().PolicyFingerprint();
        var changed = Options(); changed.StopBytes = 2_000_000_000;
        Assert.NotEqual(original,changed.PolicyFingerprint());
        changed = Options(); changed.LeasePath = "/app/other.lock";
        Assert.NotEqual(original,changed.PolicyFingerprint());
        changed = Options(); changed.InventoryPath = "/app/other.json";
        Assert.NotEqual(original,changed.PolicyFingerprint());
        Assert.NotEqual(original,Options().PolicyFingerprint(legacy:true));
        Assert.Equal(original,Options().PolicyFingerprint());
    }

    private static GenerationStorageOptions Options() => new()
    {
        InventoryPath = "/app/inventory.json", LeasePath = "/app/lease.lock", DatabaseOverheadBytesPerPick = 1_000_000,
        ArtifactRoots = LocalGenerationStorage.Categories.ToDictionary(k => k, _ => Array.Empty<string>())
    };
}
