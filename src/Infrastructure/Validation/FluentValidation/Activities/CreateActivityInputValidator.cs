using Activities.Application.UseCases.CreateActivity;
using Activities.Domain;
using FluentValidation;

namespace Infrastructure.Validation.FluentValidation.Activities;

/// <summary>
/// Structural rules of <c>POST /activities</c> (§6.2): what is required and how long it may be.
/// </summary>
/// <remarks>
/// Only shape. Which fields each status requires or forbids, whether the type can be written, and
/// whether the outcome belongs to the type's catalogue are decisions of the domain, and stay in
/// the use case and the aggregate — validating them twice is how the two copies drift. The same
/// goes for the description's length, which the domain already owns.
/// <para>
/// Two lengths do live here, and §6.2 puts them at this layer on purpose. The advisor's
/// identification is not the person code the domain validates — it is what the caller sends to
/// look one up. And the outcome's 2000-character cap is not a domain rule at all: the logical
/// contract for that column is <c>varchar(MAX)</c> (DEC-3), and the cap only exists while some
/// institutions still have the narrower column.
/// </para>
/// </remarks>
public sealed class CreateActivityInputValidator
    : AbstractValidator<CreateActivityInputDto>, IStructuralValidator<CreateActivityInputDto>
{
    /// <summary>Narrowest <c>negact_descripcion</c> found across the institutions (drift C1).</summary>
    private const int LegacyOutcomeMaxLength = 2000;

    public CreateActivityInputValidator()
    {
        RuleFor(input => input.DealId)
            .GreaterThan(0)
            .WithMessage("Deal id is required and must be greater than 0.");

        RuleFor(input => input.Status)
            .NotEmpty()
            .WithMessage("Status is required.");

        RuleFor(input => input.Type)
            .NotEmpty()
            .WithMessage("Type is required.");

        RuleFor(input => input.AdvisorIdentification)
            .NotEmpty()
            .WithMessage("Advisor identification is required.")
            .MaximumLength(ActivityLimits.AdvisorIdentificationMaxLength)
            .WithMessage($"Advisor identification cannot exceed {ActivityLimits.AdvisorIdentificationMaxLength} characters.");

        RuleFor(input => input.ActivityDate)
            .NotNull()
            .WithMessage("Activity date is required.");

        // Description's own 500-character limit is NOT here: §6.2 assigns it to the domain, and
        // Description.Create already reports it with the limit attached as an attribute, which is
        // what the monolith adapter uses to build its Spanish message.
        RuleFor(input => input.Outcome)
            .MaximumLength(LegacyOutcomeMaxLength)
            .WithMessage($"Outcome cannot exceed {LegacyOutcomeMaxLength} characters.");
    }
}
