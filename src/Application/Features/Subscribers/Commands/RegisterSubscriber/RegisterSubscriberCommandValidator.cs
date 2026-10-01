using FluentValidation;

namespace LinerNotes.Application.Features.Subscribers.Commands.RegisterSubscriber;

/// <summary>
/// FluentValidation validator enforcing subscriber registration constraints.
/// </summary>
public sealed class RegisterSubscriberCommandValidator : AbstractValidator<RegisterSubscriberCommand>
{
    public RegisterSubscriberCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(256).WithMessage("Email address must not exceed 256 characters.");

        RuleFor(x => x.TimeZone)
            .NotEmpty().WithMessage("Time zone is required.")
            .MaximumLength(64).WithMessage("Time zone must not exceed 64 characters.");

        RuleFor(x => x.DeliveryDay)
            .IsInEnum().WithMessage("A valid delivery day must be selected.");

        RuleFor(x => x.DeliveryHourUtc)
            .InclusiveBetween(0, 23).WithMessage("Delivery hour UTC must be between 0 and 23.");
    }
}
