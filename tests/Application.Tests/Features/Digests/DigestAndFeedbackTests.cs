using AutoMapper;
using FluentValidation.TestHelper;
using LinerNotes.Application.Common.Exceptions;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.Application.Common.Mappings;
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
    private readonly IMapper _mapper;

    public DigestAndFeedbackTests()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        _mapper = config.CreateMapper();
    }

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

        var handler = new GetLatestDigestQueryHandler(repo, _mapper);
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

        var handler = new GetLatestDigestQueryHandler(repo, _mapper);
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

        repo.GetRecommendationByIdAsync(recId, userId, Arg.Any<CancellationToken>())
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

        repo.GetRecommendationByIdAsync(recId, userId, Arg.Any<CancellationToken>())
            .Returns((WeeklyRecommendation?)null);

        var handler = new RecordRecommendationFeedbackCommandHandler(repo, unitOfWork);
        var command = new RecordRecommendationFeedbackCommand(recId, userId, UserFeedback.Disliked, null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(command, CancellationToken.None));

        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
