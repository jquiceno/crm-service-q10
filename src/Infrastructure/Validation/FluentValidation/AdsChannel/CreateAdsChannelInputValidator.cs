using AdsChannel.Application.UseCases.CreateAdsChannel;
using FluentValidation;

namespace Infrastructure.Validation.FluentValidation.AdsChannel;

public sealed class CreateAdsChannelInputValidator
    : AbstractValidator<CreateAdsChannelInputDto>, IStructuralValidator<CreateAdsChannelInputDto>
{
    public CreateAdsChannelInputValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);
    }
}
