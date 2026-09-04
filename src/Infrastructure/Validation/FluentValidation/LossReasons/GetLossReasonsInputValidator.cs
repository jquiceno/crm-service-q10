using FluentValidation;
using LossReason.Application.UseCases.GetLossReasons;
using LossReason.Domain.Aggregates;

namespace Infrastructure.Validation.FluentValidation.LossReasons;

public sealed class GetLossReasonsInputValidator
    : AbstractValidator<GetLossReasonsInputDto>, IStructuralValidator<GetLossReasonsInputDto>
{
    // Plain message, not a domain error: this is a filter, so a too-long search text is a malformed
    // request, not a broken invariant of the catalog. Only the number is shared with the domain --
    // searching for text longer than the longest possible name can never match a row.
    //
    // No NotEmpty rule either: on the filter a blank search means "do not filter", so requiring it
    // would turn the unfiltered listing into a 400.
    public GetLossReasonsInputValidator()
    {
        RuleFor(x => x.Search)
            .MaximumLength(LossReasonAggregate.NameMaxLength)
            .WithMessage($"Search text must not exceed {LossReasonAggregate.NameMaxLength} characters.");
    }
}
