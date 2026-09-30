using LinerNotes.DataAccess.Persistence;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Taste;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed class AppDbContextModelTests
{
    private static AppDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public void ModelBuilder_ShouldIncludeAllEntitySets()
    {
        using var context = CreateContext(nameof(ModelBuilder_ShouldIncludeAllEntitySets));
        var model = context.Model;

        Assert.NotNull(model.FindEntityType(typeof(User)));
        Assert.NotNull(model.FindEntityType(typeof(UserMusicConnection)));
        Assert.NotNull(model.FindEntityType(typeof(WeeklyDigest)));
        Assert.NotNull(model.FindEntityType(typeof(WeeklyRecommendation)));
        Assert.NotNull(model.FindEntityType(typeof(TasteSignal)));
        Assert.NotNull(model.FindEntityType(typeof(Track)));
        Assert.NotNull(model.FindEntityType(typeof(Artist)));
        Assert.NotNull(model.FindEntityType(typeof(Album)));
    }

    [Fact]
    public async Task SoftDeleteFilter_ShouldExcludeDeletedUsersByDefault()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var activeUser = User.Create("active@example.com", "Europe/London");
        var deletedUser = User.Create("deleted@example.com", "Europe/London");
        deletedUser.SoftDelete();

        context.Users.AddRange(activeUser, deletedUser);
        await context.SaveChangesAsync();

        using var queryContext = CreateContext(dbName);
        var visibleUsers = await queryContext.Users.ToListAsync();
        var allUsers = await queryContext.Users.IgnoreQueryFilters().ToListAsync();

        Assert.Single(visibleUsers);
        Assert.Equal("active@example.com", visibleUsers[0].Email);

        Assert.Equal(2, allUsers.Count);
    }

    [Fact]
    public async Task WeeklyDigest_ShouldPersistIsoWeekAndRecommendations()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var user = User.Create("listener@example.com", "UTC");
        var track = Track.Create("Paranoid Android", "Radiohead", "OK Computer", "mbid-12345");
        var isoWeek = new IsoWeek(2026, 40);

        var digest = WeeklyDigest.Create(
            user.Id,
            isoWeek,
            new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 4, 23, 59, 59, DateTimeKind.Utc));

        var breakdown = new ScoreBreakdown(
            finalScore: 0.85,
            tagSimilarityRaw: 0.90,
            tagSimilarityScore: 0.45,
            popularityRaw: 0.20,
            popularityPenalty: 0.04,
            noveltyRaw: 0.88,
            noveltyBoost: 0.176,
            feedbackPenaltyRaw: 0.0,
            feedbackPenalty: 0.0,
            matchedTags: new[] { new MatchedTagContribution("art rock", 0.9, 0.8, 0.72) });

        digest.AddRecommendation(track, breakdown, rank: 1);

        context.Users.Add(user);
        context.Tracks.Add(track);
        context.WeeklyDigests.Add(digest);
        await context.SaveChangesAsync();

        using var queryContext = CreateContext(dbName);
        var loadedDigest = await queryContext.WeeklyDigests
            .Include(d => d.Recommendations)
                .ThenInclude(r => r.Track)
            .FirstOrDefaultAsync(d => d.Id == digest.Id);

        Assert.NotNull(loadedDigest);
        Assert.Equal("2026-W40", loadedDigest.Week.Value);
        Assert.Single(loadedDigest.Recommendations);

        var loadedRec = loadedDigest.Recommendations.First();
        Assert.Equal(1, loadedRec.Rank);
        Assert.Equal("Paranoid Android", loadedRec.Track.Title);
        Assert.Equal(0.85, loadedRec.ScoreBreakdown.FinalScore);
        Assert.Contains("Shared tags (art rock)", loadedRec.WhyThisPick());
    }

    [Fact]
    public async Task TasteSignal_ShouldPersistProvenanceAndTargetValues()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var userId = Guid.NewGuid();
        var signal = TasteSignal.CreateSeedTag(userId, "Post-Punk", 0.8, "Initial profile setup");

        context.TasteSignals.Add(signal);
        await context.SaveChangesAsync();

        using var queryContext = CreateContext(dbName);
        var loadedSignal = await queryContext.TasteSignals.FirstOrDefaultAsync(s => s.Id == signal.Id);

        Assert.NotNull(loadedSignal);
        Assert.Equal(TasteTargetType.Tag, loadedSignal.TargetType);
        Assert.Equal("Post-Punk", loadedSignal.TargetValue);
        Assert.Equal("post-punk", loadedSignal.NormalizedTargetValue);
        Assert.Equal(TasteSignalSource.InitialSeedManual, loadedSignal.Source);
        Assert.Equal(0.8, loadedSignal.Weight);
    }
}
