using LinerNotes.Domain.Discovery;
using Xunit;
namespace LinerNotes.Domain.Tests;
public sealed class DiscoveryCoverageTests
{
    [Fact]
    public void CompleteSelectionReceiptCanExceedTheOld32KbLimit()
    {
        var now = DateTimeOffset.UtcNow;
        var batch = new DiscoveryBatch(Guid.NewGuid(), "coverage", DiscoveryBatchKind.OnDemand, 0, now);
        string receipt = "{\"details\":\"" + new string('x', 40_000) + "\"}";
        batch.RecordEvidence(null, "prepared-public-v1", receipt);
        Assert.Equal(receipt, batch.CoverageJson);
    }
    [Fact]
    public void CoverageBoundCountsUtf8BytesAndRemainsEnforced()
    {
        var batch = new DiscoveryBatch(Guid.NewGuid(), "coverage", DiscoveryBatchKind.OnDemand, 0, DateTimeOffset.UtcNow);
        string receipt = "{\"details\":\"" + new string('é', DiscoveryBatch.MaximumCoverageBytes / 2) + "\"}";
        Assert.Throws<ArgumentException>(() => batch.RecordEvidence(null, "prepared-public-v1", receipt));
    }
}
