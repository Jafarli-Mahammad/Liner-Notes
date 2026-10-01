using FluentValidation.TestHelper;
using LinerNotes.Application.Common.Exceptions;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.Application.DTOs.Taste;
using LinerNotes.Application.Features.Taste.Commands.SeedTasteProfile;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Taste;
using NSubstitute;
using Xunit;

namespace LinerNotes.Application.Tests.Features.Taste;

public class SeedTasteProfileTests
{
    [Fact]
    public void Validator_ShouldFail_WhenTotalSeedsAreLessThanThree()
    {
        var validator = new SeedTasteProfileCommandValidator();
        var command = new SeedTasteProfileCommand(
            UserId: Guid.NewGuid(),
            Tags: new List<SeedTagDto>
            {
                new("shoegaze", 0.9)
            },
            Artists: new List<SeedArtistDto>
            {
                new("Slowdive", 0.8)
            }); // total 2 items

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x);
    }

    [Fact]
    public void Validator_ShouldFail_WhenTotalSeedsExceedFive()
    {
        var validator = new SeedTasteProfileCommandValidator();
        var command = new SeedTasteProfileCommand(
            UserId: Guid.NewGuid(),
            Tags: new List<SeedTagDto>
            {
                new("shoegaze", 0.9),
                new("dream pop", 0.8),
                new("post-punk", 0.7)
            },
            Artists: new List<SeedArtistDto>
            {
                new("Slowdive", 0.8),
                new("Cocteau Twins", 0.9),
                new("The Cure", 0.7)
            }); // total 6 items

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x);
    }

    [Fact]
    public void Validator_ShouldPass_WhenTotalSeedsAreBetweenThreeAndFive()
    {
        var validator = new SeedTasteProfileCommandValidator();
        var command = new SeedTasteProfileCommand(
            UserId: Guid.NewGuid(),
            Tags: new List<SeedTagDto>
            {
                new("shoegaze", 0.9),
                new("dream pop", 0.8)
            },
            Artists: new List<SeedArtistDto>
            {
                new("Slowdive", 0.8),
                new("Cocteau Twins", 0.9)
            }); // total 4 items

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validator_ShouldFail_WhenWeightIsOutOfRange()
    {
        var validator = new SeedTasteProfileCommandValidator();
        var command = new SeedTasteProfileCommand(
            UserId: Guid.NewGuid(),
            Tags: new List<SeedTagDto>
            {
                new("shoegaze", 1.5), // invalid weight > 1.0
                new("dream pop", 0.8),
                new("ambient", 0.5)
            },
            Artists: new List<SeedArtistDto>());

        var result = validator.TestValidate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Handler_ShouldPersistTasteSignals_WhenUserExists()
    {
        var userId = Guid.NewGuid();
        var user = new User("tester@example.com", "UTC", id: userId);
        var userRepo = Substitute.For<IUserRepository>();
        var tasteRepo = Substitute.For<ITasteSignalRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        userRepo.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        var handler = new SeedTasteProfileCommandHandler(userRepo, tasteRepo, unitOfWork);
        var command = new SeedTasteProfileCommand(
            UserId: userId,
            Tags: new List<SeedTagDto>
            {
                new("post-punk", 0.9),
                new("coldwave", 0.7)
            },
            Artists: new List<SeedArtistDto>
            {
                new("Joy Division", 0.95),
                new("The Sound", 0.8)
            });

        var count = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(4, count);
        await tasteRepo.Received(1).AddRangeAsync(
            Arg.Is<IReadOnlyList<TasteSignal>>(list => list.Count == 4),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handler_ShouldThrowNotFoundException_WhenUserDoesNotExist()
    {
        var userId = Guid.NewGuid();
        var userRepo = Substitute.For<IUserRepository>();
        var tasteRepo = Substitute.For<ITasteSignalRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        userRepo.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var handler = new SeedTasteProfileCommandHandler(userRepo, tasteRepo, unitOfWork);
        var command = new SeedTasteProfileCommand(
            UserId: userId,
            Tags: new List<SeedTagDto> { new("ambient", 0.8), new("drone", 0.7), new("experimental", 0.6) },
            Artists: new List<SeedArtistDto>());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(command, CancellationToken.None));

        await tasteRepo.DidNotReceive().AddRangeAsync(Arg.Any<IReadOnlyList<TasteSignal>>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
