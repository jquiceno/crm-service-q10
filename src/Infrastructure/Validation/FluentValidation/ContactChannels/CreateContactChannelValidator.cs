using ContactChannel.Application.UseCases.CreateContactChannel;
using ContactChannel.Domain.Aggregates;
using FluentValidation;

namespace Infrastructure.Validation.FluentValidation.ContactChannels;

public sealed class CreateContactChannelValidator
    : AbstractValidator<CreateContactChannelInputDto>, IStructuralValidator<CreateContactChannelInputDto>
{
    public CreateContactChannelValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("The contact channel name is required.")
            .MaximumLength(ContactChannelAggregate.NameMaxLength)
            .WithMessage(
                $"The contact channel name cannot exceed {ContactChannelAggregate.NameMaxLength} characters.");
    }
}
