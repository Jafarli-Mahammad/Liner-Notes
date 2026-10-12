using FluentValidation;
using FluentValidation.Results;
using LinerNotes.Application.Common.Behaviors;
using MediatR;
using NSubstitute;
using Xunit;

namespace LinerNotes.Application.Tests.Common.Behaviors;

public sealed class ValidationCancellationTests
{
    public sealed record Command : IRequest<string>;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_ForwardsCancellationToNextHandler(bool hasValidator)
    {
        using var cancellation = new CancellationTokenSource();
        var validator = Substitute.For<IValidator<Command>>();
        validator.ValidateAsync(Arg.Any<ValidationContext<Command>>(), cancellation.Token)
            .Returns(new ValidationResult());
        var behavior = new ValidationBehavior<Command, string>(hasValidator ? [validator] : []);
        CancellationToken received = default;
        RequestHandlerDelegate<string> next = token =>
        {
            received = token;
            return Task.FromResult("complete");
        };

        Assert.Equal("complete", await behavior.Handle(new Command(), next, cancellation.Token));
        Assert.Equal(cancellation.Token, received);
    }
}
