using LinerNotes.Application.DTOs.Subscribers;
using LinerNotes.Application.Features.Subscribers.Commands.DeleteUserAccount;
using LinerNotes.Application.Features.Subscribers.Queries.GetSubscriberProfile;
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

public class SubscribersControllerTests
{
    private readonly ISender _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthService _authService;
    private readonly SubscribersController _controller;
    private readonly Guid _testUserId = Guid.NewGuid();

    public SubscribersControllerTests()
    {
        _mediator = Substitute.For<ISender>();
        _currentUser = Substitute.For<ICurrentUserService>();
        _authService = Substitute.For<IAuthService>();
        _currentUser.UserId.Returns(_testUserId);

        _controller = new SubscribersController(_authService);

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
    public async Task GetCurrentProfile_WhenProfileExists_ReturnsOk()
    {
        var profile = new SubscriberDto(
            Id: _testUserId,
            Email: "subscriber@example.com",
            TimeZone: "UTC",
            DeliveryDay: DigestDeliveryDay.Sunday,
            DeliveryHourUtc: 8,
            NextDigestAt: DateTime.UtcNow.AddDays(7),
            CreatedAt: DateTime.UtcNow);

        _mediator.Send(Arg.Is<GetSubscriberProfileQuery>(q => q.UserId == _testUserId), Arg.Any<CancellationToken>())
            .Returns(profile);

        var result = await _controller.GetCurrentProfile(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<SubscriberDto>(okResult.Value);
        Assert.Equal("subscriber@example.com", dto.Email);
    }

    [Fact]
    public async Task DeleteAccount_WhenUserExists_ReturnsNoContent()
    {
        _mediator.Send(Arg.Is<DeleteUserAccountCommand>(c => c.UserId == _testUserId), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _controller.DeleteAccount(CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        await _authService.Received(1).DeleteUserAsync(_testUserId);
    }

    [Fact]
    public async Task DeleteAccount_WhenUserNotFound_ReturnsNotFound()
    {
        _mediator.Send(Arg.Any<DeleteUserAccountCommand>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _controller.DeleteAccount(CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
    }
}
