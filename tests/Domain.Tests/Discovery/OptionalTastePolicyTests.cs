using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Discovery;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Taste;
using Xunit;

namespace LinerNotes.Domain.Tests.Discovery;

public sealed class OptionalTastePolicyTests
{
    [Fact]
    public void FavoriteRecordingRemainsASeedButIsNotRecommendedBackToItsOwner()
    {
        var profile = Guid.NewGuid(); var recording = Guid.NewGuid();
        var signal = TasteSignal.ForProfile(profile, null, TasteTargetType.Track, "Fixture recording", 1, TasteSignalSource.InitialSeedManual, "Fixture");
        signal.SetCatalogSelection(Guid.NewGuid(), recording, "{}");
        var effective = EffectiveTasteInputs.FromProfile(profile, [signal], [], new());
        Assert.Single(effective.PositiveSignals); Assert.Contains("mbid:" + recording.ToString("D"), effective.ExcludedAliases);
    }

    [Fact]
    public void RecordingFavoriteKeepsItsCanonicalSelectionSnapshot()
    {
        var signal = TasteSignal.ForProfile(Guid.NewGuid(), null, TasteTargetType.Track, "Fixture recording", 1, TasteSignalSource.InitialSeedManual, "Fixture");
        var canonical = Guid.NewGuid();
        signal.SetCatalogSelection(Guid.NewGuid(), canonical, "{\"version\":\"fixture\"}");
        Assert.Equal(canonical, signal.CanonicalEntityId);
    }

    [Fact]
    public void SkippedUnknownAndExplicitNoPreferenceRemainDistinctAndImportsKeepAnsweredValues()
    {
        var account = new TasteOnboarding(Goal: "favorites", Vocals: "no-preference");
        var guest = new TasteOnboarding(Goal: "new-artists", Exploration: "surprise", Vocals: "skipped", AllStyles: true);
        Assert.True(account.ConflictsWith(guest));
        var merged = account.FillUnanswered(guest);
        Assert.Equal("favorites", merged.Goal); Assert.Equal("surprise", merged.Exploration);
        Assert.Equal("no-preference", merged.Vocals); Assert.True(merged.AllStyles);
        Assert.Null(account.Exploration); Assert.Equal("skipped", guest.Vocals);
        Assert.Throws<ArgumentException>(() => new TasteOnboarding(Goal: "personality").Validate());
    }

    [Fact]
    public void AcquisitionWindowsCoverEverySavedSelectionRatherThanAnAlphabeticalPrefix()
    {
        var saved = Enumerable.Range(0, 503).ToArray();
        var windows = Enumerable.Range(0, 26).SelectMany(round => DiscoverySelectionPolicy.Window(saved, 20, round)).ToHashSet();
        Assert.Equal(saved.Length, windows.Count);
        Assert.Equal(20, DiscoverySelectionPolicy.Window(saved, 20, long.MaxValue).Count);
    }

    [Fact]
    public void LargeTagGroupRemainsFiniteAndDoesNotGainUnboundedInfluence()
    {
        var profile = Guid.NewGuid();
        var signals = Enumerable.Range(0, 1000).Select(i => TasteSignal.ForProfile(profile, null, TasteTargetType.Tag,
            "tag " + i, 1, TasteSignalSource.InitialSeedManual, "Fixture")).ToArray();
        var vector = DiscoveryTasteMaterializer.Build(signals, []);
        Assert.Equal(1000, vector.Weights.Count);
        Assert.Equal(1, vector.Weights.Values.Sum(w => w * w), 10);
    }

    [Fact]
    public void LargeTasteSnapshotIsBoundedAndReplaysWithTheFullOriginalMagnitude()
    {
        var scorer = new BaselineAScorer();
        var taste = WeightedTagVector.FromDictionary(Enumerable.Range(0, 1000).ToDictionary(i => "tag " + i, _ => 1d));
        var candidate = new BaselineACandidate(Track.Create("Fixture", "Fixture artist"),
            WeightedTagVector.FromDictionary(new Dictionary<string, double> { ["tag 0"] = 1 }), ScoreEvidence.Empty);
        var score = scorer.ScoreForBatch(candidate, taste, false, Guid.NewGuid(), new());
        Assert.Single(score.Snapshot!.TasteWeights);
        Assert.Equal(999, score.Snapshot.UnmatchedTasteSquared);
        Assert.Equal(score.FinalScore, scorer.Replay(score.Snapshot).FinalScore, 12);
        Assert.Equal(score.TagSimilarityRaw, scorer.Replay(score.Snapshot).TagSimilarityRaw, 12);
    }

    [Fact]
    public void AlbumFallbackInfluenceIsStoredAppliedAndExplained()
    {
        var scorer = new BaselineAScorer(); var tags = WeightedTagVector.FromDictionary(new Dictionary<string, double> { ["jazz"] = 1 });
        var evidence = ScoreEvidence.Empty with
        {
            SimilarityInfluence = .35,
            AlbumFallbacks = [new(Guid.NewGuid(), "Fixture album", Guid.NewGuid(), "Fixture credit", .35)]
        };
        var score = scorer.ScoreForBatch(new(Track.Create("Fixture", "Other artist"), tags, evidence), tags, false, Guid.NewGuid(), new());
        Assert.Equal(.35, score.TagSimilarityScore, 12);
        Assert.Contains("Fixture album", score.GenerateExplanation()); Assert.Contains("reduced", score.GenerateExplanation());
        Assert.Equal(score.FinalScore, scorer.Replay(score.Snapshot!).FinalScore, 12);
    }

    [Fact]
    public void GoalsAndExplorationChangeSelectionWithoutChangingStoredScores()
    {
        var close = Pick("Close", 1, false); var far = Pick("Far", .1, true);
        Assert.Equal("Close", DiscoverySelectionPolicy.Rank([far, close], new(Goal: "favorites"), 1)[0].Track.Title);
        Assert.Equal("Far", DiscoverySelectionPolicy.Rank([far, close], new(Goal: "new-artists"), 1)[0].Track.Title);
        Assert.Equal("Far", DiscoverySelectionPolicy.Rank([close, far], new(Exploration: "surprise"), 1)[0].Track.Title);
        Assert.Equal("Close", DiscoverySelectionPolicy.Rank([far, close], new(Goal: "favorites", Exploration: "surprise", AllStyles: true), 1)[0].Track.Title);
    }

    [Fact]
    public void VocalPreferenceUsesOnlyExplicitRecordingEvidenceAndConflictsStayUnknown()
    {
        var response = new StoredResponse("Fixture", "recording.tags", "fixture", "Synthetic", null, null);
        StoredTag Tag(string name) => new(name, new("1", "1", null), null, response, null, 1);
        var evidence = ScoreEvidence.Empty with { TagScope = "recording", CandidateTags = [Tag("instrumental")] };
        Assert.Equal(1, DiscoverySelectionPolicy.VocalMatch(evidence, "instrumental"));
        Assert.Equal(-1, DiscoverySelectionPolicy.VocalMatch(evidence, "vocals"));
        Assert.Equal(0, DiscoverySelectionPolicy.VocalMatch(evidence with { TagScope = "artist" }, "vocals"));
        Assert.Equal(0, DiscoverySelectionPolicy.VocalMatch(evidence with { CandidateTags = [Tag("instrumental"), Tag("vocals")] }, "vocals"));
        Assert.Equal(0, DiscoverySelectionPolicy.VocalMatch(ScoreEvidence.Empty, "instrumental"));
    }

    private static BaselineAPick Pick(string name, double overlap, bool novel) => new(Track.Create(name, name),
        new() { FinalScore = overlap, TagSimilarityRaw = overlap, NoveltyRaw = novel ? 1 : 0 });
}
