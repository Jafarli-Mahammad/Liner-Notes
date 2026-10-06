using FluentValidation.TestHelper;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.Application.Features.Subscribers.Commands.RegisterSubscriber;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using NSubstitute;
using Xunit;

namespace LinerNotes.Application.Tests.Features.Subscribers;

public class RegisterSubscriberTests
{
    [Fact]
    public void Validator_ShouldHaveError_WhenEmailIsInvalid()
    {
        var validator = new RegisterSubscriberCommandValidator();
        var command = new RegisterSubscriberCommand(
            Email: "not-an-email",
            TimeZone: "UTC",
            DeliveryDay: DigestDeliveryDay.Friday,
            DeliveryHourUtc: 9);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validator_ShouldHaveError_WhenDeliveryHourIsOutOfRange()
    {
        var validator = new RegisterSubscriberCommandValidator();
        var command = new RegisterSubscriberCommand(
            Email: "user@example.com",
            TimeZone: "UTC",
            DeliveryDay: DigestDeliveryDay.Friday,
            DeliveryHourUtc: 25);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.DeliveryHourUtc);
    }

    [Fact]
    public void Validator_ShouldNotHaveError_WhenCommandIsValid()
    {
        var validator = new RegisterSubscriberCommandValidator();
        var command = new RegisterSubscriberCommand(
            Email: "listener@example.com",
            TimeZone: "Europe/London",
            DeliveryDay: DigestDeliveryDay.Sunday,
            DeliveryHourUtc: 8);

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Handler_ShouldCreateAndPersistUser_WhenEmailIsAvailable()
    {
        var userRepository = Substitute.For<IUserRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        userRepository.GetByEmailAsync("newlistener@example.com", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var handler = new RegisterSubscriberCommandHandler(userRepository, unitOfWork);
        var command = new RegisterSubscriberCommand(
            Email: "newlistener@example.com",
            TimeZone: "Europe/Berlin",
            DeliveryDay: DigestDeliveryDay.Saturday,
            DeliveryHourUtc: 10);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("newlistener@example.com", result.Email);
        Assert.Equal("Europe/Berlin", result.TimeZone);
        Assert.Equal(DigestDeliveryDay.Saturday, result.DeliveryDay);
        Assert.Equal(10, result.DeliveryHourUtc);
        Assert.NotNull(result.NextDigestAt);

        await userRepository.Received(1).AddAsync(Arg.Is<User>(u => u.Email == "newlistener@example.com"), Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handler_ShouldThrowException_WhenEmailAlreadyExists()
    {
        var userRepository = Substitute.For<IUserRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var existingUser = new User("existing@example.com", "UTC");
        userRepository.GetByEmailAsync("existing@example.com", Arg.Any<CancellationToken>())
            .Returns(existingUser);

        var handler = new RegisterSubscriberCommandHandler(userRepository, unitOfWork);
        var command = new RegisterSubscriberCommand(
            Email: "existing@example.com",
            TimeZone: "UTC",
            DeliveryDay: DigestDeliveryDay.Sunday,
            DeliveryHourUtc: 8);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(command, CancellationToken.None));

        Assert.Contains("already exists", ex.Message);
        await userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
