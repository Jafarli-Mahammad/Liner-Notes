using LinerNotes.DataAccess.Persistence;
using LinerNotes.DataAccess.Persistence.Repositories;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Taste;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed class ComprehensiveRepositoryTests
{
    private static AppDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task WeeklyDigestRepository_GetRecentDigestsForUserAsync_ShouldReturnOrderedByWeekDescending()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var userId = Guid.NewGuid();
        var digestW38 = WeeklyDigest.Create(userId, IsoWeek.From(2026, 38), DateTime.UtcNow.AddDays(-21), DateTime.UtcNow.AddDays(-14));
        var digestW39 = WeeklyDigest.Create(userId, IsoWeek.From(2026, 39), DateTime.UtcNow.AddDays(-14), DateTime.UtcNow.AddDays(-7));
        var digestW40 = WeeklyDigest.Create(userId, IsoWeek.From(2026, 40), DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

        var repo = new WeeklyDigestRepository(context);
        await repo.AddAsync(digestW38);
        await repo.AddAsync(digestW39);
        await repo.AddAsync(digestW40);
        await context.SaveChangesAsync();

        var recent = await repo.GetRecentDigestsForUserAsync(userId, count: 2);

        Assert.Equal(2, recent.Count);
        Assert.Equal("2026-W40", recent[0].Week.Value);
        Assert.Equal("2026-W39", recent[1].Week.Value);
    }

    [Fact]
    public async Task WeeklyDigestRepository_GetRecommendationByIdAsync_ShouldReturnWithTrackAndRating()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var userId = Guid.NewGuid();
        var digest = WeeklyDigest.Create(userId, IsoWeek.From(2026, 40), DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
        var track = Track.Create("Losing My Religion", "R.E.M.");
        var breakdown = new ScoreBreakdown();
        var recommendation = digest.AddRecommendation(track, breakdown, rank: 1);
        recommendation.RecordFeedback(UserFeedback.Liked, "Classic song", rating: 9);

        context.WeeklyDigests.Add(digest);
        context.Tracks.Add(track);
        await context.SaveChangesAsync();

        var repo = new WeeklyDigestRepository(context);
        var loadedRec = await repo.GetRecommendationByIdAsync(recommendation.Id, userId);

        Assert.NotNull(loadedRec);
        Assert.Equal(recommendation.Id, loadedRec.Id);
        Assert.Equal(9, loadedRec.Rating);
        Assert.Equal(UserFeedback.Liked, loadedRec.Feedback);
        Assert.Equal("Classic song", loadedRec.FeedbackComment);
        Assert.NotNull(loadedRec.Track);
        Assert.Equal("Losing My Religion", loadedRec.Track.Title);
    }

    [Fact]
    public async Task TasteSignalRepository_DeleteSignalsForUserAsync_ShouldOnlyDeleteTargetUser()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        var s1 = TasteSignal.CreateSeedTag(user1, "synthpop", 1.0, "seed");
        var s2 = TasteSignal.CreateSeedArtist(user1, "Depeche Mode", 1.0, "seed");
        var s3 = TasteSignal.CreateSeedTag(user2, "synthpop", 1.0, "seed");

        var repo = new TasteSignalRepository(context);
        await repo.AddRangeAsync(new[] { s1, s2, s3 });
        await context.SaveChangesAsync();

        await repo.DeleteSignalsForUserAsync(user1);
        await context.SaveChangesAsync();

        var remainingUser1 = await repo.GetByUserIdAsync(user1);
        var remainingUser2 = await repo.GetByUserIdAsync(user2);

        Assert.Empty(remainingUser1);
        Assert.Single(remainingUser2);
        Assert.Equal(user2, remainingUser2[0].UserId);
    }

    [Fact]
    public async Task TrackRepository_GetByMbidAndFindByArtistAndTitle_ShouldRetrieveMatches()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var trackMbid = Track.Create("Enjoy the Silence", "Depeche Mode", "Violator", "mbid-depeche-1");
        var trackTitle = Track.Create("Policy of Truth", "Depeche Mode", "Violator");

        var repo = new TrackRepository(context);
        await repo.AddRangeAsync(new[] { trackMbid, trackTitle });
        await context.SaveChangesAsync();

        var foundMbid = await repo.GetByMbidAsync("mbid-depeche-1");
        var foundTitle = await repo.FindByArtistAndTitleAsync("depeche mode", "policy of truth");
        var missing = await repo.FindByArtistAndTitleAsync("Unknown", "Track");

        Assert.NotNull(foundMbid);
        Assert.Equal(trackMbid.Id, foundMbid.Id);

        Assert.NotNull(foundTitle);
        Assert.Equal(trackTitle.Id, foundTitle.Id);

        Assert.Null(missing);
    }
}
