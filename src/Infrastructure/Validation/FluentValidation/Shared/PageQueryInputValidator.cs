using FluentValidation;
using Shared.Application.Dtos;

namespace Infrastructure.Validation.FluentValidation.Shared;

public sealed class PageQueryInputValidator : AbstractValidator<PageQueryInputDto>, IStructuralValidator<PageQueryInputDto>
{
    public PageQueryInputValidator()
    {
        RuleFor(x => x.PageIndex)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Page index must be greater than or equal to 0.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, PageQueryInputDto.MaxPageSize)
            .WithMessage($"Page size must be between 1 and {PageQueryInputDto.MaxPageSize}.");
    }
}
