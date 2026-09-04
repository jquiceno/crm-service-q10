using FluentValidation;
using Shared.Application.Dtos;

namespace Infrastructure.Validation.FluentValidation.Shared;

public sealed class ConsecutiveIdInputValidator
    : AbstractValidator<ConsecutiveIdInputDto>, IStructuralValidator<ConsecutiveIdInputDto>
{
    public ConsecutiveIdInputValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("Id must be greater than 0.");
    }
}
