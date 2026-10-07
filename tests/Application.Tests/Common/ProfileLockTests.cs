using System.Globalization;
using Xunit;

namespace LinerNotes.Application.Tests.Common;

public sealed class ProfileLockTests
{
    // Metadata-only checks; no ranking, acquisition or held-out response inspection.
    [Fact]
    public void ProposedSplitHasDeclaredCountsAndPreservesSparseFounderProfiles()
    {
        var profiles = Phase2ProfileMembership.Proposed();
        Assert.Equal(6, profiles.Count(p => p.Set == EvaluationSet.Founder));
        Assert.Equal(16, profiles.Count(p => p.Set == EvaluationSet.Development));
        Assert.Equal(16, profiles.Count(p => p.Set == EvaluationSet.HeldOut));
        Assert.Equal(5, profiles.Count(p => p.Set == EvaluationSet.Founder && p.Seeds.Count == 1));
        Assert.Equal(13, profiles.Count(p => p.Set == EvaluationSet.Synthetic));
        var hash = ProfileLock.ComputeHash(profiles);
        Assert.Equal(64, hash.Length);
        // A test reference validates mechanics; it does not grant real membership approval.
        var locked = new ProfileLock(profiles, hash, "synthetic-lock-validation-test-only");
        Assert.Equal(hash, locked.Hash);
    }

    [Fact]
    public void HashIsOrderAndCultureIndependentAndSeedsAreSnapshotted()
    {
        var profiles = Phase2ProfileMembership.Proposed().ToArray();
        var mutable = new List<string>(profiles[0].Seeds);
        profiles[0] = profiles[0] with { Seeds = mutable };
        var hash = ProfileLock.ComputeHash(profiles);
        var locked = new ProfileLock(profiles, hash, "synthetic-lock-validation-test-only");
        mutable.Add("changed after lock");
        Assert.Equal(hash, ProfileLock.ComputeHash(locked.Profiles));
        var before = CultureInfo.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "", "en-US", "tr-TR", "az-Latn-AZ" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                Assert.Equal(hash, ProfileLock.ComputeHash(locked.Profiles.Reverse()
                    .Select(p => p with { Seeds = p.Seeds.Reverse().ToArray() })));
            }
        }
        finally { CultureInfo.CurrentCulture = before; }
        Assert.Equal(ProfileLock.Canonical("Éliane Radigue"), ProfileLock.Canonical("E\u0301liane Radigue"));
    }

    [Fact]
    public void MissingApprovalChangedHashAndSplitLeakageAreRejected()
    {
        var profiles = Phase2ProfileMembership.Proposed().ToArray();
        var hash = ProfileLock.ComputeHash(profiles);
        Assert.Throws<ArgumentException>(() => new ProfileLock(profiles, hash, ""));
        Assert.Throws<ArgumentException>(() => new ProfileLock(profiles, new string('0', 64), "test-only"));
        int held = Array.FindIndex(profiles, p => p.Set == EvaluationSet.HeldOut);
        string development = profiles.First(p => p.Set == EvaluationSet.Development).Seeds[0];
        profiles[held] = profiles[held] with { Seeds = [development, "other held-out seed"] };
        Assert.Throws<ArgumentException>(() => new ProfileLock(profiles, ProfileLock.ComputeHash(profiles), "test-only"));
    }

    [Fact]
    public void InvalidProfileCountsGenresAndDuplicateSeedsAreRejected()
    {
        var original = Phase2ProfileMembership.Proposed();
        var changes = new List<IReadOnlyList<EvaluationProfile>>
        {
            original.Skip(1).ToArray(),
            original.Select((p, i) => i == 0 ? p with { Genre = "unknown" } : p).ToArray(),
            original.Select((p, i) => i == 0 ? p with { Seeds = ["same", " SAME "] } : p).ToArray(),
            original.Append(original[0]).ToArray()
        };
        foreach (var profiles in changes)
            Assert.Throws<ArgumentException>(() => new ProfileLock(profiles, ProfileLock.ComputeHash(profiles), "test-only"));
    }

    [Fact]
    public async Task RejectingHttpHandlerFailsEveryAttempt()
    {
        using var handler = new OutboundRejectingHandler();
        using var client = new HttpClient(handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAsync("https://example.invalid"));
        Assert.Equal(1, handler.Attempts);
    }
}
