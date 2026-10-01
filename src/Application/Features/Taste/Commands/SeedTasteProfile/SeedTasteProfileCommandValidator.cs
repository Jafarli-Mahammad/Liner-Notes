using FluentValidation;

namespace LinerNotes.Application.Features.Taste.Commands.SeedTasteProfile;

/// <summary>
/// FluentValidation validator enforcing the 3–5 seed item hybrid onboarding constraint.
/// </summary>
public sealed class SeedTasteProfileCommandValidator : AbstractValidator<SeedTasteProfileCommand>
{
    public SeedTasteProfileCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        RuleFor(x => x)
            .Must(x => (x.Tags?.Count ?? 0) + (x.Artists?.Count ?? 0) >= 3)
            .WithMessage("At least 3 seed items (artists or tags) are required to build a discovery profile.")
            .Must(x => (x.Tags?.Count ?? 0) + (x.Artists?.Count ?? 0) <= 5)
            .WithMessage("Maximum of 5 seed items are allowed during initial onboarding to prevent profile dilution.");

        RuleForEach(x => x.Tags).ChildRules(tag =>
        {
            tag.RuleFor(t => t.Tag)
                .NotEmpty().WithMessage("Seed tag cannot be empty.")
                .MaximumLength(100).WithMessage("Seed tag must not exceed 100 characters.");

            tag.RuleFor(t => t.Weight)
                .InclusiveBetween(0.1, 1.0).WithMessage("Seed tag weight must be between 0.1 and 1.0.");
        });

        RuleForEach(x => x.Artists).ChildRules(artist =>
        {
            artist.RuleFor(a => a.ArtistName)
                .NotEmpty().WithMessage("Seed artist name cannot be empty.")
                .MaximumLength(256).WithMessage("Seed artist name must not exceed 256 characters.");

            artist.RuleFor(a => a.Weight)
                .InclusiveBetween(0.1, 1.0).WithMessage("Seed artist weight must be between 0.1 and 1.0.");
        });
    }
}
