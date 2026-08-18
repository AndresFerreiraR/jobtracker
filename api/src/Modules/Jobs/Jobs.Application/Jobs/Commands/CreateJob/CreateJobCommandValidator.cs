using FluentValidation;

namespace Jobs.Application.Jobs.Commands.CreateJob;

internal sealed class CreateJobCommandValidator : AbstractValidator<CreateJobCommand>
{
    public CreateJobCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(4000);

        RuleFor(x => x.CustomerId)
            .NotEmpty();

        RuleFor(x => x.Address)
            .NotNull()
            .ChildRules(a =>
            {
                a.RuleFor(x => x.Street).NotEmpty().MaximumLength(200);
                a.RuleFor(x => x.City).NotEmpty().MaximumLength(120);
                a.RuleFor(x => x.State).NotEmpty().MaximumLength(60);
                a.RuleFor(x => x.ZipCode)
                    .NotEmpty()
                    .MaximumLength(10)
                    .Matches(@"^[A-Za-z0-9][A-Za-z0-9\- ]{2,9}$")
                    .WithMessage("'{PropertyName}' must be 3–10 alphanumeric characters (spaces and hyphens allowed).");
                a.RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
                a.RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
            });
    }
}
