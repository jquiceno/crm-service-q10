using FluentValidation;

namespace Application.UseCases.Example;

public sealed class ExampleInputValidator : AbstractValidator<ExampleInputDto>
{
    public ExampleInputValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");
    }
}
