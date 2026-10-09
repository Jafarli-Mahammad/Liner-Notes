using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Taste;
using Xunit;

namespace LinerNotes.Domain.Tests.Taste;

public sealed class TasteVectorMaterializerTests
{
    [Fact]
    public void Materialization_SumsEveryPositiveSeedAndManualTagInStableOrder()
    {
        var id = Guid.NewGuid();
        TasteSignal[] signals = [TasteSignal.CreateSeedArtist(id, "Artist", 0.5, "manual"),
            TasteSignal.CreateSeedArtist(id, "Artist", 1, "manual"), TasteSignal.CreateSeedTag(id, "rock", 0.25, "manual")];
        var seeds = new Dictionary<string, WeightedTagVector> { ["artist"] = WeightedTagVector.FromDictionary(
            new Dictionary<string, double> { ["rock"] = 1, ["ambient"] = 0.5 }) };
        var taste = TasteVectorMaterializer.Build(signals, seeds);
        Assert.Equal(1.75, taste["rock"]);
        Assert.Equal(0.75, taste["ambient"]);
        Assert.Equal(taste, TasteVectorMaterializer.Build(signals.Reverse().ToArray(), seeds));
        Assert.Equal(new[] { "ambient", "rock" }, taste.Weights.Keys);
    }

    [Fact]
    public void LatestFeedback_RemovesStaleLikeAndRejectionWithoutPenalizingSiblingTracks()
    {
        var id = Guid.NewGuid();
        var older = new WeeklyRecommendation(Guid.NewGuid(), id, Track.Create("Track", "Artist", mbid: "identifier"), 1, new());
        older.RecordFeedback(UserFeedback.Disliked, rating: 1);
        var latest = new WeeklyRecommendation(Guid.NewGuid(), id, Track.Create("Track", "Artist"), 1,
            new ScoreBreakdown { MatchedTags = [new("rock", 1, 1, 1)] });
        latest.RecordFeedback(UserFeedback.Liked, rating: 10);
        var stale = TasteSignal.CreateFromDislike(id, "mbid:identifier", TasteTargetType.Track, $"rec:{older.Id} - old dislike");
        var effective = EffectiveTasteInputs.From(id, [stale], [older, latest], new());
        Assert.Empty(effective.ExcludedAliases);
        Assert.Contains(effective.PositiveSignals, s => s.TargetType == TasteTargetType.Artist && s.Weight == 1);
        Assert.Contains(effective.PositiveSignals, s => s.TargetType == TasteTargetType.Tag && s.Weight == 0.5);
        latest.RecordFeedback(UserFeedback.Disliked, rating: 2);
        effective = EffectiveTasteInputs.From(id, [stale], [older, latest], new());
        Assert.Empty(effective.PositiveSignals);
        Assert.Contains("artist:track", effective.ExcludedAliases);
        Assert.DoesNotContain("artist:sibling", effective.ExcludedAliases);
        Assert.Empty(effective.FamiliarArtists);
    }

    [Fact]
    public void InvalidOrOverflowingSignals_AreRejected()
    {
        var id = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => TasteVectorMaterializer.Build(
            [TasteSignal.CreateSeedTag(id, "rock", double.NaN, "manual")], new Dictionary<string, WeightedTagVector>()));
        Assert.Throws<ArgumentException>(() => TasteVectorMaterializer.Build(
            [TasteSignal.CreateSeedTag(id, "rock", double.MaxValue, "manual"), TasteSignal.CreateSeedTag(id, "rock", double.MaxValue, "manual")],
            new Dictionary<string, WeightedTagVector>()));
    }
}
