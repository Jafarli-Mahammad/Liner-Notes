using LinerNotes.Application.DTOs.Taste;
using LinerNotes.Application.Features.Taste.Commands.SeedTasteProfile;
using LinerNotes.Application.Features.Taste.Queries.GetUserTasteSignals;
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

public class TasteControllerTests
{
    private readonly ISender _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly TasteController _controller;
    private readonly Guid _testUserId = Guid.NewGuid();

    public TasteControllerTests()
    {
        _mediator = Substitute.For<ISender>();
        _currentUser = Substitute.For<ICurrentUserService>();
        _currentUser.UserId.Returns(_testUserId);

        _controller = new TasteController();

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
    public async Task SeedProfile_ValidRequest_CallsMediatorAndReturnsOk()
    {
        var request = new SeedTasteProfileRequest(
            Tags: new List<SeedTagDto> { new("post-punk"), new("shoegaze"), new("krautrock") },
            Artists: new List<SeedArtistDto>());

        _mediator.Send(Arg.Is<SeedTasteProfileCommand>(c => c.UserId == _testUserId && c.Tags.Count == 3), Arg.Any<CancellationToken>())
            .Returns(3);

        var result = await _controller.SeedProfile(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetTasteSignals_ReturnsUserSignals()
    {
        var expectedSignals = new List<TasteSignalDto>
        {
            new(Guid.NewGuid(), _testUserId, TasteTargetType.Tag, "post-punk", "post-punk", 1.0, TasteSignalSource.InitialSeedManual, "seed", DateTime.UtcNow)
        };

        _mediator.Send(Arg.Is<GetUserTasteSignalsQuery>(q => q.UserId == _testUserId), Arg.Any<CancellationToken>())
            .Returns(expectedSignals);

        var result = await _controller.GetTasteSignals(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<TasteSignalDto>>(okResult.Value);
        Assert.Single(list);
    }
}
