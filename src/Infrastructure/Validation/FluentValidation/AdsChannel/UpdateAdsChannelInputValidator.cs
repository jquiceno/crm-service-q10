using AdsChannel.Application.UseCases.UpdateAdsChannel;
using FluentValidation;

namespace Infrastructure.Validation.FluentValidation.AdsChannel;

public sealed class UpdateAdsChannelInputValidator :
    AbstractValidator<UpdateAdsChannelInputDto>, IStructuralValidator<UpdateAdsChannelInputDto>
{
    public UpdateAdsChannelInputValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);
    }
}
