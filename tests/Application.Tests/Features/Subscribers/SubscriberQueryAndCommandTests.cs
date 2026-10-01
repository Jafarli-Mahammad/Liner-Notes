using AutoMapper;
using LinerNotes.Application.Common.Exceptions;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.Application.Common.Mappings;
using LinerNotes.Application.Features.Subscribers.Commands.DeleteUserAccount;
using LinerNotes.Application.Features.Subscribers.Queries.GetSubscriberProfile;
using LinerNotes.Domain.Digest;
using NSubstitute;
using Xunit;

namespace LinerNotes.Application.Tests.Features.Subscribers;

public class SubscriberQueryAndCommandTests
{
    private readonly IMapper _mapper;

    public SubscriberQueryAndCommandTests()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        _mapper = config.CreateMapper();
    }

    [Fact]
    public async Task GetSubscriberProfile_ReturnsProfile_WhenUserExists()
    {
        var userId = Guid.NewGuid();
        var user = new User("found@example.com", "UTC", id: userId);
        var repo = Substitute.For<IUserRepository>();
        repo.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        var handler = new GetSubscriberProfileQueryHandler(repo, _mapper);
        var result = await handler.Handle(new GetSubscriberProfileQuery(userId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(userId, result.Id);
        Assert.Equal("found@example.com", result.Email);
    }

    [Fact]
    public async Task GetSubscriberProfile_ReturnsNull_WhenUserNotFound()
    {
        var userId = Guid.NewGuid();
        var repo = Substitute.For<IUserRepository>();
        repo.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var handler = new GetSubscriberProfileQueryHandler(repo, _mapper);
        var result = await handler.Handle(new GetSubscriberProfileQuery(userId), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteUserAccount_SoftDeletesUser_WhenUserExists()
    {
        var userId = Guid.NewGuid();
        var user = new User("user@example.com", "UTC", id: userId);
        var repo = Substitute.For<IUserRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        repo.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        var handler = new DeleteUserAccountCommandHandler(repo, unitOfWork);
        var result = await handler.Handle(new DeleteUserAccountCommand(userId), CancellationToken.None);

        Assert.True(result);
        Assert.True(user.IsDeleted);
        repo.Received(1).Update(user);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteUserAccount_ThrowsNotFoundException_WhenUserDoesNotExist()
    {
        var userId = Guid.NewGuid();
        var repo = Substitute.For<IUserRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        repo.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var handler = new DeleteUserAccountCommandHandler(repo, unitOfWork);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new DeleteUserAccountCommand(userId), CancellationToken.None));

        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
