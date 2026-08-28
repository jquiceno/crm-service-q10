using Activities.Application.UseCases.GetActivities;
using FluentValidation;

namespace Infrastructure.Validation.FluentValidation.Activities;

/// <summary>
/// Structural rules of <c>GET /activities</c> (§6.1).
/// </summary>
/// <remarks>
/// The "at least one filter" rule is the legacy endpoint's, and it lives here rather than in the
/// use case: without it the query would list every activity of the institution, so it is the
/// request that is wrong, not the domain.
/// </remarks>
public sealed class GetActivitiesInputValidator
    : AbstractValidator<GetActivitiesInputDto>, IStructuralValidator<GetActivitiesInputDto>
{
    public GetActivitiesInputValidator()
    {
        RuleFor(input => input.DealId)
            .GreaterThan(0)
            .When(input => input.DealId.HasValue)
            .WithMessage("Deal id must be greater than 0.");

        RuleFor(input => input.OpportunityId)
            .GreaterThan(0)
            .When(input => input.OpportunityId.HasValue)
            .WithMessage("Opportunity id must be greater than 0.");

        RuleFor(input => input.DealStateId)
            .GreaterThan(0)
            .When(input => input.DealStateId.HasValue)
            .WithMessage("Deal state id must be greater than 0.");

        // Hung on DealId rather than on the DTO itself: a rule on the whole object reports the
        // whole object as the offending value, which is noise in the payload.
        RuleFor(input => input.DealId)
            .Must((input, _) =>
                input.DealId.HasValue || input.OpportunityId.HasValue || input.DealStateId.HasValue)
            .WithMessage("At least one of deal id, opportunity id or deal state id is required.");
    }
}
