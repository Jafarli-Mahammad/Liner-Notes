using FluentValidation.TestHelper;
using LinerNotes.Application.Common.Exceptions;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.Application.Features.Digests.Commands.RecordFeedback;
using LinerNotes.Application.Features.Digests.Queries.GetLatestDigest;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Scoring;
using NSubstitute;
using Xunit;

namespace LinerNotes.Application.Tests.Features.Digests;

public class DigestAndFeedbackTests
{
    [Fact]
    public async Task GetLatestDigest_ReturnsLatestDigest_WhenUserHasDigests()
    {
        var userId = Guid.NewGuid();
        var digest = new WeeklyDigest(userId, new IsoWeek(2026, 40), DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
        var track = new Track("Blue Monday", "New Order");
        var breakdown = new ScoreBreakdown(0.9, 0.8, 0.8, 0.2, 0.1, 0.5, 0.1, 0, 0, Array.Empty<MatchedTagContribution>());
        digest.AddRecommendation(track, breakdown, 1);

        var repo = Substitute.For<IWeeklyDigestRepository>();
        repo.GetRecentDigestsForUserAsync(userId, 1, Arg.Any<CancellationToken>())
            .Returns(new List<WeeklyDigest> { digest });

        var handler = new GetLatestDigestQueryHandler(repo);
        var result = await handler.Handle(new GetLatestDigestQuery(userId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("2026-W40", result.Week);
        Assert.Single(result.Recommendations);
    }

    [Fact]
    public async Task GetLatestDigest_ReturnsNull_WhenUserHasNoDigests()
    {
        var userId = Guid.NewGuid();
        var repo = Substitute.For<IWeeklyDigestRepository>();
        repo.GetRecentDigestsForUserAsync(userId, 1, Arg.Any<CancellationToken>())
            .Returns(new List<WeeklyDigest>());

        var handler = new GetLatestDigestQueryHandler(repo);
        var result = await handler.Handle(new GetLatestDigestQuery(userId), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public void RecordFeedbackValidator_ShouldFail_WhenRecommendationIdOrUserIdIsEmpty()
    {
        var validator = new RecordRecommendationFeedbackCommandValidator();
        var command = new RecordRecommendationFeedbackCommand(
            RecommendationId: Guid.Empty,
            UserId: Guid.Empty,
            Feedback: UserFeedback.Liked,
            Comment: null);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.RecommendationId);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public void RecordFeedbackValidator_ShouldPass_ForValidInput()
    {
        var validator = new RecordRecommendationFeedbackCommandValidator();
        var command = new RecordRecommendationFeedbackCommand(
            RecommendationId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            Feedback: UserFeedback.AlreadyKnown,
            Comment: "Great track, already familiar.");

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task RecordFeedbackHandler_UpdatesRecommendation_WhenFound()
    {
        var recId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var digestId = Guid.NewGuid();
        var track = new Track("Transmission", "Joy Division");
        var breakdown = new ScoreBreakdown(0.85, 0.9, 0.9, 0.1, 0.05, 0.8, 0.08, 0, 0, Array.Empty<MatchedTagContribution>());
        var recommendation = new WeeklyRecommendation(digestId, userId, track, 1, breakdown, id: recId);

        var repo = Substitute.For<IWeeklyDigestRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task<bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<bool>>>()(call.Arg<CancellationToken>()));

        repo.GetRecommendationForUpdateAsync(recId, userId, Arg.Any<CancellationToken>())
            .Returns(recommendation);

        var handler = new RecordRecommendationFeedbackCommandHandler(repo, unitOfWork);
        var command = new RecordRecommendationFeedbackCommand(
            RecommendationId: recId,
            UserId: userId,
            Feedback: UserFeedback.Liked,
            Comment: "Loved the bassline!");

        var success = await handler.Handle(command, CancellationToken.None);

        Assert.True(success);
        Assert.Equal(UserFeedback.Liked, recommendation.Feedback);
        Assert.Equal("Loved the bassline!", recommendation.FeedbackComment);
        Assert.NotNull(recommendation.FeedbackGivenAt);

        repo.Received(1).UpdateRecommendation(recommendation);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordFeedbackHandler_ThrowsNotFoundException_WhenRecommendationDoesNotExist()
    {
        var recId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var repo = Substitute.For<IWeeklyDigestRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task<bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<bool>>>()(call.Arg<CancellationToken>()));

        repo.GetRecommendationForUpdateAsync(recId, userId, Arg.Any<CancellationToken>())
            .Returns((WeeklyRecommendation?)null);

        var handler = new RecordRecommendationFeedbackCommandHandler(repo, unitOfWork);
        var command = new RecordRecommendationFeedbackCommand(recId, userId, UserFeedback.Disliked, null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(command, CancellationToken.None));

        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(-5)]
    public void RecordFeedbackValidator_ShouldFail_WhenRatingIsOutOfRange(int invalidRating)
    {
        var validator = new RecordRecommendationFeedbackCommandValidator();
        var command = new RecordRecommendationFeedbackCommand(
            RecommendationId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            Feedback: UserFeedback.Liked,
            Rating: invalidRating);

        var result = validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Rating);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(10)]
    public void RecordFeedbackValidator_ShouldPass_WhenRatingIsWithinRange(int validRating)
    {
        var validator = new RecordRecommendationFeedbackCommandValidator();
        var command = new RecordRecommendationFeedbackCommand(
            RecommendationId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            Feedback: UserFeedback.Liked,
            Rating: validRating);

        var result = validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Rating);
    }

    [Theory]
    [InlineData(UserFeedback.Liked, 1)]
    [InlineData(UserFeedback.Liked, 3)]
    [InlineData(UserFeedback.Disliked, 7)]
    [InlineData(UserFeedback.Disliked, 10)]
    public void RecordFeedbackValidator_RejectsContradictoryRatings(UserFeedback feedback, int rating)
    {
        var validator = new RecordRecommendationFeedbackCommandValidator();
        var result = validator.TestValidate(new RecordRecommendationFeedbackCommand(Guid.NewGuid(), Guid.NewGuid(), feedback, Rating: rating));
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task RecordFeedbackHandler_CreatesTasteSignals_WhenTasteSignalRepoProvided()
    {
        var recId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var digestId = Guid.NewGuid();
        var track = new Track("Disorder", "Joy Division");
        var matchedTags = new List<MatchedTagContribution>
        {
            new("post-punk", 1.0, 0.9, 0.9),
            new("new wave", 0.8, 0.7, 0.56)
        };
        var breakdown = new ScoreBreakdown(0.85, 0.9, 0.9, 0.1, 0.05, 0.8, 0.08, 0, 0, matchedTags);
        var recommendation = new WeeklyRecommendation(digestId, userId, track, 1, breakdown, id: recId);

        var digestRepo = Substitute.For<IWeeklyDigestRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task<bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<bool>>>()(call.Arg<CancellationToken>()));
        var tasteRepo = Substitute.For<ITasteSignalRepository>();

        digestRepo.GetRecommendationForUpdateAsync(recId, userId, Arg.Any<CancellationToken>())
            .Returns(recommendation);

        var handler = new RecordRecommendationFeedbackCommandHandler(digestRepo, unitOfWork, tasteRepo);
        var command = new RecordRecommendationFeedbackCommand(
            RecommendationId: recId,
            UserId: userId,
            Feedback: UserFeedback.Liked,
            Rating: 9,
            Comment: "Instant favorite");

        var success = await handler.Handle(command, CancellationToken.None);

        Assert.True(success);
        Assert.Equal(9, recommendation.Rating);
        Assert.Equal(UserFeedback.Liked, recommendation.Feedback);

        await tasteRepo.Received(1).DeleteSignalsByContextPrefixAsync(
            userId,
            $"rec:{recId}",
            Arg.Any<CancellationToken>());

        await tasteRepo.Received(1).AddRangeAsync(
            Arg.Is<IEnumerable<LinerNotes.Domain.Taste.TasteSignal>>(signals =>
                signals.Any(s => s.TargetType == TasteTargetType.Artist && s.TargetValue == "Joy Division") &&
                signals.Any(s => s.TargetType == TasteTargetType.Tag && s.TargetValue == "post-punk")),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
