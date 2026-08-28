using FluentValidation;
using LossReason.Application.UseCases.UpdateLossReason;
using LossReason.Domain.Aggregates;
using LossReason.Domain.Errors;

namespace Infrastructure.Validation.FluentValidation.LossReasons;

public sealed class UpdateLossReasonInputValidator
    : AbstractValidator<UpdateLossReasonInputDto>, IStructuralValidator<UpdateLossReasonInputDto>
{
    // Same two Name rules as CreateLossReasonInputValidator, and for the same reason: the messages
    // and state come from the domain error catalog so the HTTP layer answers exactly what the
    // aggregate would. IsActive is non-nullable here, so the deserializer already rejects a null.
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
    }
}
