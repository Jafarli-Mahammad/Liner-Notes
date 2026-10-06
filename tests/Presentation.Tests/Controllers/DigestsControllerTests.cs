using LinerNotes.Application.DTOs.Digests;
using LinerNotes.Application.Features.Digests.Commands.RecordFeedback;
using LinerNotes.Application.Features.Digests.Queries.GetLatestDigest;
using LinerNotes.Application.Services;
using LinerNotes.Domain.Enums;
using LinerNotes.Presentation.Controllers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace LinerNotes.Presentation.Tests.Controllers;

public class DigestsControllerTests
{
    private readonly ISender _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly DigestsController _controller;
    private readonly Guid _testUserId = Guid.NewGuid();

    public DigestsControllerTests()
    {
        _mediator = Substitute.For<ISender>();
        _currentUser = Substitute.For<ICurrentUserService>();
        _currentUser.UserId.Returns(_testUserId);

        _controller = new DigestsController();

        var services = new ServiceCollection();
        services.AddSingleton(_mediator);
        services.AddSingleton(_currentUser);
        var serviceProvider = services.BuildServiceProvider();

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = serviceProvider }
        };
    }

    [Fact]
    public async Task GetLatestDigest_WhenDigestExists_ReturnsOk()
    {
        var expectedDigest = new WeeklyDigestDto(
            Id: Guid.NewGuid(),
            UserId: _testUserId,
            Week: "2026-W40",
            WeekStartDate: DateTime.UtcNow.Date,
            WeekEndDate: DateTime.UtcNow.Date.AddDays(7),
            Status: DigestStatus.Sent,
            SentAt: DateTime.UtcNow,
            Recommendations: new List<WeeklyRecommendationDto>());

        _mediator.Send(Arg.Is<GetLatestDigestQuery>(q => q.UserId == _testUserId), Arg.Any<CancellationToken>())
            .Returns(expectedDigest);

        var result = await _controller.GetLatestDigest(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var digest = Assert.IsType<WeeklyDigestDto>(okResult.Value);
        Assert.Equal("2026-W40", digest.Week);
    }

    [Fact]
    public async Task GetLatestDigest_WhenNoDigestExists_ReturnsNotFound()
    {
        _mediator.Send(Arg.Is<GetLatestDigestQuery>(q => q.UserId == _testUserId), Arg.Any<CancellationToken>())
            .Returns((WeeklyDigestDto?)null);

        var result = await _controller.GetLatestDigest(CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
    }

    [Fact]
    public async Task RecordFeedback_ValidRecommendation_ReturnsOk()
    {
        var recId = Guid.NewGuid();
        var request = new RecordFeedbackRequest(UserFeedback.Liked, "Great track!");

        _mediator.Send(Arg.Is<RecordRecommendationFeedbackCommand>(c =>
            c.RecommendationId == recId &&
            c.UserId == _testUserId &&
            c.Feedback == UserFeedback.Liked), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _controller.RecordFeedback(recId, request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task RecordFeedback_RecommendationNotFound_ReturnsNotFound()
    {
        var recId = Guid.NewGuid();
        var request = new RecordFeedbackRequest(UserFeedback.Disliked);

        _mediator.Send(Arg.Any<RecordRecommendationFeedbackCommand>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _controller.RecordFeedback(recId, request, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
    }
}
