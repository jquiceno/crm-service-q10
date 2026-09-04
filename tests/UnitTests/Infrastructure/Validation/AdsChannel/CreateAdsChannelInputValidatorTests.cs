using AdsChannel.Application.UseCases.CreateAdsChannel;
using AdsChannel.Domain.Aggregates;
using FluentValidation.TestHelper;
using Infrastructure.Validation.FluentValidation.AdsChannel;
using Xunit;

namespace UnitTests.Infrastructure.Validation.AdsChannel;

public sealed class CreateAdsChannelInputValidatorTests
{
    private readonly CreateAdsChannelInputValidator _sut = new();

    [Fact]
    public void Validate_WithValidName_ReturnsValid()
    {
        var result = _sut.TestValidate(new CreateAdsChannelInputDto("Google Ads", true));

        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_WithEmptyName_HasErrorOnName()
    {
        var result = _sut.TestValidate(new CreateAdsChannelInputDto("", true));

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_WithNullName_HasErrorOnName()
    {
        var result = _sut.TestValidate(new CreateAdsChannelInputDto(null, true));

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_WithNameOverMaxLength_HasErrorOnName()
    {
        var name = new string('a', AdsChannelAggregate.MaxNameLength + 1);

        var result = _sut.TestValidate(new CreateAdsChannelInputDto(name, true));

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_WithNameAtMaxLength_ReturnsValid()
    {
        var name = new string('a', AdsChannelAggregate.MaxNameLength);

        var result = _sut.TestValidate(new CreateAdsChannelInputDto(name, true));

        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }
}
