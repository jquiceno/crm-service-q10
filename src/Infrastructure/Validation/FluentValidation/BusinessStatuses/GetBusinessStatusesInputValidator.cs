using BusinessStatus.Application.UseCases.GetBusinessStatuses;
using BusinessStatus.Domain.Aggregates;
using FluentValidation;

namespace Infrastructure.Validation.FluentValidation.BusinessStatuses;

public sealed class GetBusinessStatusesInputValidator
    : AbstractValidator<GetBusinessStatusesInputDto>, IStructuralValidator<GetBusinessStatusesInputDto>
{
    public GetBusinessStatusesInputValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(BusinessStatusAggregate.MaxNameLength)
            .WithMessage($"Name must not exceed {BusinessStatusAggregate.MaxNameLength} characters.");

        RuleFor(x => x.Kind)
            .IsInEnum()
            .WithMessage("Kind must be one of All, Intermediate or Terminal.");
    }
}
