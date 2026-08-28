using FluentValidation;
using LossReason.Application.UseCases.CreateLossReason;
using LossReason.Domain.Aggregates;
using LossReason.Domain.Errors;

namespace Infrastructure.Validation.FluentValidation.LossReasons;

public sealed class CreateLossReasonInputValidator
    : AbstractValidator<CreateLossReasonInputDto>, IStructuralValidator<CreateLossReasonInputDto>
{
    // Messages and state come from the domain error catalog so the HTTP layer answers with the
    // exact same text, ErrorType and Attributes the aggregate would produce. WithState is what
    // FluentRequestValidationAdapter reads to rebuild the ValidationError: without it the
    // "max" attribute of NameTooLong is lost and the client has to parse the message.
    public CreateLossReasonInputValidator()
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
