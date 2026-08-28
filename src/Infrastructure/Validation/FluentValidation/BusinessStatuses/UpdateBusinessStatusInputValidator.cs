using BusinessStatus.Application.UseCases.UpdateBusinessStatus;
using BusinessStatus.Domain.Aggregates;
using BusinessStatus.Domain.ValueObjects;
using FluentValidation;

namespace Infrastructure.Validation.FluentValidation.BusinessStatuses;

/// <summary>
/// Structural rules for the update request: the same ones as the create request, because the update
/// replaces the whole resource. It does not reject the terminal percentages 0 and 100, nor does it
/// know whether the stored status is terminal: INV-1 and INV-2 are domain invariants the aggregate
/// owns, so the client gets a domain error naming the rule instead of a generic range failure.
/// </summary>
public sealed class UpdateBusinessStatusInputValidator
    : AbstractValidator<UpdateBusinessStatusInputDto>, IStructuralValidator<UpdateBusinessStatusInputDto>
{
    public UpdateBusinessStatusInputValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Business status name is required.")
            .MaximumLength(BusinessStatusAggregate.MaxNameLength)
            .WithMessage($"Business status name must not exceed {BusinessStatusAggregate.MaxNameLength} characters.");

        RuleFor(x => x.Percentage)
            .NotNull()
            .WithMessage("Percentage is required.")
            .InclusiveBetween(BusinessStatusAggregate.MinPercentage, BusinessStatusAggregate.MaxPercentage)
            .WithMessage($"Percentage must be between {BusinessStatusAggregate.MinPercentage} and {BusinessStatusAggregate.MaxPercentage}.");

        RuleFor(x => x.Color)
            .Matches(StatusColor.Pattern)
            .WithMessage($"Color must be {StatusColor.Length} hexadecimal characters without '#'.")
            .When(x => !string.IsNullOrWhiteSpace(x.Color));
    }
}
