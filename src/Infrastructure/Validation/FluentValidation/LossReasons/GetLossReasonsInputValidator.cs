using FluentValidation;
using LossReason.Application.UseCases.GetLossReasons;
using LossReason.Domain.Aggregates;
using LossReason.Domain.Errors;

namespace Infrastructure.Validation.FluentValidation.LossReasons;

public sealed class GetLossReasonsInputValidator
    : AbstractValidator<GetLossReasonsInputDto>, IStructuralValidator<GetLossReasonsInputDto>
{
    // Only the length rule: on the filter an absent or empty Name means "do not filter by name",
    // which is why NotEmpty would turn the unfiltered listing into a 400. The pagination bounds
    // belong to PageQueryInputValidator, which already covers PageQueryInputDto.
    public GetLossReasonsInputValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(LossReasonAggregate.NameMaxLength)
            .WithMessage(LossReasonErrors.NameTooLong.Message)
            .WithState(_ => LossReasonErrors.NameTooLong);
    }
}
