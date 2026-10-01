using FluentValidation;
using FluentValidation.Results;
using LinerNotes.Application.Common.Behaviors;
using AppValidationException = LinerNotes.Application.Common.Exceptions.ValidationException;
using MediatR;
using NSubstitute;
using Xunit;

namespace LinerNotes.Application.Tests.Common.Behaviors;

public class ValidationBehaviorTests
{
    public record SampleCommand(string Value) : IRequest<string>;

    [Fact]
    public async Task Handle_WithNoValidators_InvokesNextHandler()
    {
        var behavior = new ValidationBehavior<SampleCommand, string>(Enumerable.Empty<IValidator<SampleCommand>>());
        var command = new SampleCommand("Valid");

        var nextCalled = false;
        RequestHandlerDelegate<string> next = () =>
        {
            nextCalled = true;
            return Task.FromResult("Success");
        };

        var result = await behavior.Handle(command, next, CancellationToken.None);

        Assert.True(nextCalled);
        Assert.Equal("Success", result);
    }

    [Fact]
    public async Task Handle_WithValidRequest_InvokesNextHandler()
    {
        var validator = Substitute.For<IValidator<SampleCommand>>();
        validator.ValidateAsync(Arg.Any<ValidationContext<SampleCommand>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var behavior = new ValidationBehavior<SampleCommand, string>(new[] { validator });
        var command = new SampleCommand("Valid");

        var nextCalled = false;
        RequestHandlerDelegate<string> next = () =>
        {
            nextCalled = true;
            return Task.FromResult("Success");
        };

        var result = await behavior.Handle(command, next, CancellationToken.None);

        Assert.True(nextCalled);
        Assert.Equal("Success", result);
    }

    [Fact]
    public async Task Handle_WithInvalidRequest_ThrowsValidationExceptionWithErrors()
    {
        var failure = new ValidationFailure("Value", "Value is required.");
        var validator = Substitute.For<IValidator<SampleCommand>>();
        validator.ValidateAsync(Arg.Any<ValidationContext<SampleCommand>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new[] { failure }));

        var behavior = new ValidationBehavior<SampleCommand, string>(new[] { validator });
        var command = new SampleCommand("");

        var nextCalled = false;
        RequestHandlerDelegate<string> next = () =>
        {
            nextCalled = true;
            return Task.FromResult("Success");
        };

        var ex = await Assert.ThrowsAsync<AppValidationException>(() =>
            behavior.Handle(command, next, CancellationToken.None));

        Assert.False(nextCalled);
        Assert.True(ex.Errors.ContainsKey("Value"));
        Assert.Contains("Value is required.", ex.Errors["Value"]);
    }
}
