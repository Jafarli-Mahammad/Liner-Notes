using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Discovery;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Taste;
using Xunit;

namespace LinerNotes.Domain.Tests.Discovery;

public sealed class DiscoveryOwnershipTests
{
    [Fact]
    public void DiscoverySnapshotsReplayWithoutCalendarDeliveryIdentity()
    {
        var batch = Guid.NewGuid();
        var scorer = new BaselineAScorer();
        var vector = WeightedTagVector.FromDictionary(new Dictionary<string, double> { ["jazz"] = 1 });
        var candidate = new BaselineACandidate(Track.Create("Synthetic recording", "Synthetic artist"), vector, ScoreEvidence.Empty);
        var score = scorer.ScoreForBatch(candidate, vector, false, batch, new());
        Assert.Null(score.Snapshot!.Week);
        Assert.Equal(batch, score.Snapshot.DiscoveryBatchId);
        Assert.Equal("discovery-score-v2", score.Snapshot.PayloadVersion);
        Assert.Equal(score.FinalScore, scorer.Replay(score.Snapshot).FinalScore);
        Assert.Equal(score.GenerateExplanation(), scorer.Replay(score.Snapshot).GenerateExplanation());
        Assert.Throws<ArgumentException>(() => scorer.Replay(score.Snapshot with { DiscoveryBatchId = null }));
    }

    [Fact]
    public void ProfileInputsIgnoreOtherOwnersAndGuestRecommendationsHaveNoAccountOrDelivery()
    {
        var profile = Guid.NewGuid();
        var other = Guid.NewGuid();
        var signal = TasteSignal.ForProfile(profile, null, TasteTargetType.Tag, "jazz", 1,
            TasteSignalSource.InitialSeedManual, "Explicit guest selection");
        var foreign = TasteSignal.ForProfile(other, null, TasteTargetType.Tag, "metal", 1,
            TasteSignalSource.InitialSeedManual, "Other selection");
        var track = Track.Create("Synthetic recording", "Synthetic artist");
        var pick = WeeklyRecommendation.ForDiscovery(profile, Guid.NewGuid(), null, track, 1, new());
        var otherPick = WeeklyRecommendation.ForDiscovery(other, Guid.NewGuid(), null, track, 1, new());
        otherPick.RecordFeedback(UserFeedback.AlreadyKnown);
        var inputs = EffectiveTasteInputs.FromProfile(profile, [signal, foreign], [pick, otherPick], new());
        Assert.Same(signal, Assert.Single(inputs.PositiveSignals));
        Assert.Empty(inputs.ExcludedAliases);
        Assert.Null(pick.UserId);
        Assert.Null(pick.WeeklyDigestId);
        Assert.Equal(profile, pick.ProfileId);
    }
}
