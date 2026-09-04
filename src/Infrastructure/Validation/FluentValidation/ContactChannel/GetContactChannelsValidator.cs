using ContactChannel.Application.UseCases.GetContactChannels;
using FluentValidation;

namespace Infrastructure.Validation.FluentValidation.ContactChannel;

public sealed class GetContactChannelsValidator
    : AbstractValidator<GetContactChannelsInputDto>, IStructuralValidator<GetContactChannelsInputDto>
{
    public const int SearchMaxLength = 200;

    public GetContactChannelsValidator()
    {
        RuleFor(x => x.Search)
            .MaximumLength(SearchMaxLength)
            .WithMessage($"Search must not exceed {SearchMaxLength} characters.");
    }
}
