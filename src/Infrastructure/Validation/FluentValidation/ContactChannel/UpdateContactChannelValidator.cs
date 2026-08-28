using ContactChannel.Application.UseCases.UpdateContactChannel;
using ContactChannel.Domain.Aggregates;
using ContactChannel.Domain.Errors;
using FluentValidation;

namespace Infrastructure.Validation.FluentValidation.ContactChannel;

public sealed class UpdateContactChannelValidator
    : AbstractValidator<UpdateContactChannelInputDto>, IStructuralValidator<UpdateContactChannelInputDto>
{
    public UpdateContactChannelValidator()
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
