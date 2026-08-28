using FluentValidation;
using LossReason.Application.UseCases.CreateLossReason;

namespace Infrastructure.Validation.FluentValidation.LossReasons;

public sealed class CreateLossReasonInputValidator
    : AbstractValidator<CreateLossReasonInputDto>, IStructuralValidator<CreateLossReasonInputDto>
{
    public CreateLossReasonInputValidator()
    {
        // Only IsActive: Name is left to the aggregate, which already reports NameRequired and
        // NameTooLong on its own Property. Duplicating it here would answer the same payload
        // with two different error shapes depending on which layer ran first.
        RuleFor(x => x.IsActive)
            .NotNull()
            .WithMessage("Whether the loss reason is active is required.");
    }
}
