using LinerNotes.Application.Common.Interfaces;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly SubscribersController _controller;
    private readonly Guid _testUserId = Guid.NewGuid();

    public SubscribersControllerTests()
    {
        _mediator = Substitute.For<ISender>();
        _currentUser = Substitute.For<ICurrentUserService>();
        _authService = Substitute.For<IAuthService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task<IActionResult>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<IActionResult>>>()(call.Arg<CancellationToken>()));
        _currentUser.UserId.Returns(_testUserId);

        _controller = new SubscribersController(_authService, _unitOfWork);

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

        _authService.DeleteUserAsync(_testUserId).Returns(true);
        var result = await _controller.DeleteAccount(CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        await _authService.Received(1).DeleteUserAsync(_testUserId);
    }

    [Fact]
    public async Task DeleteAccount_WhenUserNotFound_ReturnsNotFound()
    {
        _mediator.Send(Arg.Any<DeleteUserAccountCommand>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _authService.DeleteUserAsync(_testUserId).Returns(true);
        var result = await _controller.DeleteAccount(CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
    }

    [Fact]
    public async Task DeleteAccount_ReportsCleanupAfterCommittedSql()
    {
        bool committed = false;
        var archive = Substitute.For<ILocalEmailArchive>();
        _unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task<IActionResult>>>(), Arg.Any<CancellationToken>())
            .Returns(async call => { var result = await call.Arg<Func<CancellationToken, Task<IActionResult>>>()(default); committed = true; return result; });
        archive.DeleteAsync(_testUserId, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Assert.True(committed);
            return new LocalEmailCleanupResult(false, ["copy_cleanup_unavailable"]);
        });
        _mediator.Send(Arg.Any<DeleteUserAccountCommand>(), Arg.Any<CancellationToken>()).Returns(true);
        _authService.DeleteUserAsync(_testUserId).Returns(true);
        var controller = new SubscribersController(_authService, _unitOfWork, archive) { ControllerContext = _controller.ControllerContext };
        Assert.IsType<OkObjectResult>(await controller.DeleteAccount(default));
        await archive.Received(1).DeleteAsync(_testUserId, Arg.Any<CancellationToken>());
    }
}
