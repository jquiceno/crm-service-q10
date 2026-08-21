using ContactChannel.Application.UseCases.GetContactChannels;
using FluentValidation;

namespace Infrastructure.Validation.FluentValidation.ContactChannel;

public sealed class GetContactChannelsValidator
    : AbstractValidator<GetContactChannelsInputDto>, IStructuralValidator<GetContactChannelsInputDto>
{
    public const int SearchNameMaxLength = 200;

    public GetContactChannelsValidator()
    {
        RuleFor(x => x.SearchName)
            .MaximumLength(SearchNameMaxLength)
            .WithMessage($"Search name must not exceed {SearchNameMaxLength} characters.");
    }
}
