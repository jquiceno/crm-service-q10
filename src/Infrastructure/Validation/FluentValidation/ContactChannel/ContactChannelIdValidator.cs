using ContactChannel.Application.Dtos;
using FluentValidation;

namespace Infrastructure.Validation.FluentValidation.ContactChannel;

public sealed class ContactChannelIdValidator
    : AbstractValidator<ContactChannelIdInputDto>, IStructuralValidator<ContactChannelIdInputDto>
{
    public ContactChannelIdValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("The contact channel identifier must be greater than zero.");
    }
}
