using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Taste;
using Xunit;

namespace LinerNotes.Domain.Tests;

public sealed class ResidueFeedbackMemoryTests
{
    private readonly Guid owner = Guid.NewGuid();
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private WeeklyRecommendation Pick(string artist, string title, UserFeedback feedback, int minute = 0, string? mbid = null)
    {
        var r = WeeklyRecommendation.ForDiscovery(owner, Guid.NewGuid(), null, new Track(title, artist, mbid: mbid), 1, new ScoreBreakdown());
        r.RecordFeedback(feedback, now: Start.AddMinutes(minute));
        return r;
    }
    private EffectiveTasteInputs Build(WeeklyRecommendation[] recent, WeeklyRecommendation[] history, params TasteSignal[] manual)
        => EffectiveTasteInputs.FromProfile(owner, manual, recent, new(), history.Select(FeedbackObservation.From).ToArray());

    [Fact]
    public void NoteEditsDoNotRefreshMusicalTimeAndClearIsANewMusicalState()
    {
        var r = Pick("Rock", "Old", UserFeedback.Liked);
        r.RecordFeedback(UserFeedback.Liked, "private uninterpreted prose", now: Start.AddYears(1));
        Assert.Equal(Start, r.FeedbackGivenAt);
        Assert.Equal(Start.AddYears(1), r.LastModifiedAt);
        r.RecordFeedback(UserFeedback.None, now: Start.AddYears(2));
        Assert.Equal(Start.AddYears(2), r.FeedbackGivenAt);
        var note = Pick("Other", "Note only", UserFeedback.None);
        note.RecordFeedback(UserFeedback.None, "note", now: Start);
        Assert.Null(note.FeedbackGivenAt);
    }

    [Fact]
    public void DurableRockSurvivesFortyRecentJazzLikesAndDoesNotBecomeFortyArtistVotes()
    {
        var rock = Pick("Established rock", "Old", UserFeedback.Liked);
        var jazz = Enumerable.Range(1, 40).Select(i => Pick("Recent jazz", $"New {i}", UserFeedback.Liked, i)).ToArray();
        var taste = Build(jazz.TakeLast(20).ToArray(), [rock, ..jazz]);
        Assert.Equal(new[] { "established rock", "recent jazz" }, taste.FamiliarArtists.Order().ToArray());
        Assert.Equal(2, taste.PositiveSignals.Count);
        Assert.Equal(40, taste.FeedbackSupport.Single(s => s.Artist == "recent jazz").PositiveRecordings);
        Assert.Equal(20, taste.FeedbackSupport.Single(s => s.Artist == "recent jazz").RecentPositiveRecordings);
    }

    [Fact]
    public void RepeatedIndependentDislikesSoftenLearnedEvidenceButLeaveExplicitFavoritesAndRecoverOnEdits()
    {
        var liked = Pick("Artist", "Favorite discovery", UserFeedback.Liked);
        var dislikes = Enumerable.Range(1, 3).Select(i => Pick("Artist", $"Poor {i}", UserFeedback.Disliked, i)).ToArray();
        var manual = TasteSignal.ForProfile(owner, null, TasteTargetType.Artist, "Artist", 1, TasteSignalSource.InitialSeedManual, "Explicit favorite");
        var one = Build([], [liked, dislikes[0]], manual);
        Assert.False(Assert.Single(one.FeedbackSupport).Attenuated);
        var repeated = Build([], [liked, ..dislikes], manual);
        var support = Assert.Single(repeated.FeedbackSupport);
        Assert.True(support.Attenuated);
        Assert.Equal(.4, support.LearnedWeight);
        Assert.Equal(1, repeated.PositiveSignals.Single(s => s.Source == TasteSignalSource.InitialSeedManual).Weight);
        Assert.Equal(3, repeated.ExcludedAliases.Count);
        dislikes[0].RecordFeedback(UserFeedback.Liked, now: Start.AddHours(1));
        Assert.False(Assert.Single(Build([], [liked, ..dislikes], manual).FeedbackSupport).Attenuated);
        foreach (var r in dislikes) r.RecordFeedback(UserFeedback.None, now: Start.AddHours(2));
        Assert.Empty(Build([], [liked, ..dislikes]).ExcludedAliases);
        Assert.Equal(1, Assert.Single(Build([], [liked, ..dislikes]).FeedbackSupport).LearnedWeight);
    }

    [Fact]
    public void AliasBridgesCountOnceAndLatestClearOrNeutralSupersedesOlderVotes()
    {
        var mbid = Guid.NewGuid().ToString();
        var old = Pick("Artist", "Original", UserFeedback.Disliked);
        var bridge = Pick("Artist", "Original", UserFeedback.Disliked, 1, mbid);
        var latest = Pick("Renamed artist", "Retitled", UserFeedback.Liked, 2, mbid);
        Assert.Empty(Build([], [old, bridge, latest]).ExcludedAliases);
        Assert.Equal(1, Assert.Single(Build([], [old, bridge, latest]).FeedbackSupport).PositiveRecordings);
        latest.RecordFeedback(UserFeedback.None, now: Start.AddHours(1));
        Assert.Empty(Build([], [old, bridge, latest]).PositiveSignals);
        latest.RecordFeedback(UserFeedback.None, rating: 5, now: Start.AddHours(2));
        Assert.Empty(Build([], [old, bridge, latest]).ExcludedAliases);
        Assert.Empty(Build([], [old, bridge, latest]).PositiveSignals);
    }

    [Fact]
    public void ForeignCompactHistoryCannotAffectAnotherListener()
    {
        var a = Pick("Private artist", "Private track", UserFeedback.Disliked);
        var result = EffectiveTasteInputs.FromProfile(Guid.NewGuid(), [], [], new(), [FeedbackObservation.From(a)]);
        Assert.Empty(result.FeedbackSupport);
        Assert.Empty(result.ExcludedAliases);
        Assert.Empty(result.PositiveSignals);
    }

    [Fact]
    public void KnownIsNotANegativeVoteAndLowRatingSuppressesOnlyItsRecording()
    {
        var known = Pick("Artist", "Known", UserFeedback.AlreadyKnown);
        var low = Pick("Artist", "Low", UserFeedback.None);
        low.RecordFeedback(UserFeedback.None, rating: 2);
        var taste = Build([], [known, low]);
        Assert.Equal(2, taste.ExcludedAliases.Count);
        Assert.Equal(1, Assert.Single(taste.FeedbackSupport).NegativeRecordings);
        Assert.Empty(taste.PositiveSignals);
    }
    [Theory]
    [InlineData("rock", "jazz")]
    [InlineData("hip hop", "classical")]
    [InlineData("ambient", "folk")]
    public void IndependentProfilesDoNotInferGenreAversionOrCountDuplicateAliasVotes(string established, string recent)
    {
        var a = Pick(established + " artist", "Positive", UserFeedback.Liked);
        var b = Pick(recent + " artist", "Positive", UserFeedback.Liked);
        var recording = Guid.NewGuid().ToString();
        var duplicateVotes = Enumerable.Range(0, 3).Select(i => Pick(established + " artist", "Alias " + i, UserFeedback.Disliked, i + 1, recording)).ToArray();
        var one = Build([], [a, b, ..duplicateVotes]);
        Assert.Equal(1, one.FeedbackSupport.Single(s => s.Artist.StartsWith(established)).NegativeRecordings);
        Assert.All(one.FeedbackSupport, s => Assert.False(s.Attenuated));
        var repeated = Build([], [a, b, ..duplicateVotes,
            Pick(established + " artist", "Independent one", UserFeedback.Disliked, 5),
            Pick(established + " artist", "Independent two", UserFeedback.Disliked, 6)]);
        Assert.True(repeated.FeedbackSupport.Single(s => s.Artist.StartsWith(established)).Attenuated);
        Assert.Equal(1, repeated.FeedbackSupport.Single(s => s.Artist.StartsWith(recent)).LearnedWeight);
        Assert.DoesNotContain(repeated.PositiveSignals, s => s.TargetType == TasteTargetType.Tag);
    }

    [Fact]
    public void RepeatingLikesOfTheSameTagDoesNotWeakenItsRecentContribution()
    {
        var likes = Enumerable.Range(0, 40).Select(i =>
        {
            var pick = WeeklyRecommendation.ForDiscovery(owner, Guid.NewGuid(), null, new Track("Recording " + i, "Artist"), 1,
                new ScoreBreakdown { MatchedTags = [new MatchedTagContribution("jazz", 1, 1, 1)] });
            pick.RecordFeedback(UserFeedback.Liked, now: Start.AddMinutes(i)); return pick;
        }).ToArray();
        var first = Build([likes[0]], [likes[0]]);
        var sustained = Build(likes, likes);
        Assert.Single(sustained.PositiveSignals.Where(s => s.TargetType == TasteTargetType.Tag));
        Assert.Equal(DiscoveryTasteMaterializer.Build(first.PositiveSignals, []).Weights["jazz"],
            DiscoveryTasteMaterializer.Build(sustained.PositiveSignals, []).Weights["jazz"]);
    }

}
