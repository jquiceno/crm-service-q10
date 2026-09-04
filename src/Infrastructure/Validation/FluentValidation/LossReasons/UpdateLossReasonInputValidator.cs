using FluentValidation;
using LossReason.Application.UseCases.UpdateLossReason;
using LossReason.Domain.Aggregates;
using LossReason.Domain.Errors;

namespace Infrastructure.Validation.FluentValidation.LossReasons;

public sealed class UpdateLossReasonInputValidator
    : AbstractValidator<UpdateLossReasonInputDto>, IStructuralValidator<UpdateLossReasonInputDto>
{
    // Same three rules as CreateLossReasonInputValidator, and for the same reason: the messages and
    // state come from the domain error catalog so the HTTP layer answers exactly what the aggregate
    // would. IsActive is required rather than defaulted -- a bool that is not sent would otherwise
    // arrive as false and deactivate the reason silently.
    public UpdateLossReasonInputValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage(LossReasonErrors.NameRequired.Message)
            .WithState(_ => LossReasonErrors.NameRequired);

        RuleFor(x => x.Name)
            .MaximumLength(LossReasonAggregate.NameMaxLength)
            .WithMessage(LossReasonErrors.NameTooLong.Message)
            .WithState(_ => LossReasonErrors.NameTooLong);

        RuleFor(x => x.IsActive)
            .NotNull()
            .WithMessage(LossReasonErrors.IsActiveRequired.Message)
            .WithState(_ => LossReasonErrors.IsActiveRequired);
    }
}
