using FluentValidation;
using Shared.Application.Dtos;

namespace Infrastructure.Validation.FluentValidation.Shared;

public sealed class IdInputValidator : AbstractValidator<IdInputDto>, IStructuralValidator<IdInputDto>
{
    public IdInputValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("Id must be greater than 0.");
    }
}
