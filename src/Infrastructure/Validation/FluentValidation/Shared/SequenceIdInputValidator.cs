using FluentValidation;
using Shared.Application.Dtos;

namespace Infrastructure.Validation.FluentValidation.Shared;

public sealed class SequenceIdInputValidator
    : AbstractValidator<SequenceIdInputDto>, IStructuralValidator<SequenceIdInputDto>
{
    public SequenceIdInputValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("Id must be greater than 0.");
    }
}
