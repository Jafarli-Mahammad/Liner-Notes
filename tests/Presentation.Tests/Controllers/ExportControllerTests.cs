using LinerNotes.Application.DTOs.Digests;
using LinerNotes.Application.DTOs.Export;
using LinerNotes.Application.DTOs.Subscribers;
using LinerNotes.Application.DTOs.Taste;
using LinerNotes.Application.Features.Export.Queries.GetUserDataExport;
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

public class ExportControllerTests
{
    private readonly ISender _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly ExportController _controller;
    private readonly Guid _testUserId = Guid.NewGuid();

    public ExportControllerTests()
    {
        _mediator = Substitute.For<ISender>();
        _currentUser = Substitute.For<ICurrentUserService>();
        _currentUser.UserId.Returns(_testUserId);

        _controller = new ExportController();

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
    public async Task ExportMyData_ReturnsFullUserDataExportDto()
    {
        var subscriber = new SubscriberDto(
            Id: _testUserId,
            Email: "export@example.com",
            TimeZone: "UTC",
            DeliveryDay: DigestDeliveryDay.Sunday,
            DeliveryHourUtc: 8,
            NextDigestAt: null,
            CreatedAt: DateTime.UtcNow);

        var exportDto = new UserDataExportDto(
            ExportVersion: "1.0.0",
            ExportedAtUtc: DateTime.UtcNow,
            Subscriber: subscriber,
            Connections: Array.Empty<UserMusicConnectionExportDto>(),
            TasteSignals: Array.Empty<TasteSignalDto>(),
            Digests: Array.Empty<WeeklyDigestDto>());

        _mediator.Send(Arg.Is<GetUserDataExportQuery>(q => q.UserId == _testUserId), Arg.Any<CancellationToken>())
            .Returns(exportDto);

        var result = await _controller.ExportMyData(download: false, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var data = Assert.IsType<UserDataExportDto>(okResult.Value);
        Assert.Equal("1.0.0", data.ExportVersion);
        Assert.Equal("export@example.com", data.Subscriber.Email);
    }

    [Fact]
    public async Task ExportMyData_WithDownloadTrue_SetsAttachmentHeader()
    {
        var exportDto = new UserDataExportDto(
            ExportVersion: "1.0.0",
            ExportedAtUtc: DateTime.UtcNow,
            Subscriber: new SubscriberDto(_testUserId, "test@test.com", "UTC", DigestDeliveryDay.Sunday, 8, null, DateTime.UtcNow),
            Connections: Array.Empty<UserMusicConnectionExportDto>(),
            TasteSignals: Array.Empty<TasteSignalDto>(),
            Digests: Array.Empty<WeeklyDigestDto>());

        _mediator.Send(Arg.Any<GetUserDataExportQuery>(), Arg.Any<CancellationToken>())
            .Returns(exportDto);

        var result = await _controller.ExportMyData(download: true, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var header = _controller.Response.Headers["Content-Disposition"].ToString();
        Assert.Contains("attachment", header);
        Assert.Contains(_testUserId.ToString(), header);
    }

    [Fact]
    public async Task ExportMyData_Unauthenticated_ReturnsUnauthorized()
    {
        _currentUser.UserId.Returns(Guid.Empty);

        var result = await _controller.ExportMyData(download: false, CancellationToken.None);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.Status);
    }
}
