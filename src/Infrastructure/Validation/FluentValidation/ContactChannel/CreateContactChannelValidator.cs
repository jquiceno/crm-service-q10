using ContactChannel.Application.UseCases.CreateContactChannel;
using ContactChannel.Domain.Aggregates;
using ContactChannel.Domain.Errors;
using FluentValidation;

namespace Infrastructure.Validation.FluentValidation.ContactChannel;

public sealed class CreateContactChannelValidator
    : AbstractValidator<CreateContactChannelInputDto>, IStructuralValidator<CreateContactChannelInputDto>
{
    public CreateContactChannelValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage(ContactChannelErrors.NameRequired.Message)
            .WithState(_ => ContactChannelErrors.NameRequired)
            .MaximumLength(ContactChannelAggregate.NameMaxLength)
            .WithMessage(ContactChannelErrors.NameTooLong.Message)
            .WithState(_ => ContactChannelErrors.NameTooLong);

        RuleFor(x => x.IsActive)
            .NotNull()
            .WithMessage(ContactChannelErrors.IsActiveRequired.Message)
            .WithState(_ => ContactChannelErrors.IsActiveRequired);
    }
}
