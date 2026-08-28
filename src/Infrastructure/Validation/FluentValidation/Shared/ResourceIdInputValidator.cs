using FluentValidation;
using Shared.Application.Dtos;

namespace Infrastructure.Validation.FluentValidation.Shared;

public sealed class ResourceIdInputValidator
    : AbstractValidator<ResourceIdInputDto>, IStructuralValidator<ResourceIdInputDto>
{
    public ResourceIdInputValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("The identifier must be greater than zero.");
    }
}
