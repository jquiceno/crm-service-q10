using AdsChannel.Application.UseCases.CreateAdsChannel;
using Infrastructure.Validation.FluentValidation.AdsChannel;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Validation.AdsChannel;

public sealed class CreateAdsChannelInputValidatorTests
{
    private readonly CreateAdsChannelInputValidator _sut = new();

    [Fact]
    public void Validate_WithValidName_ReturnsValid()
    {
        var result = _sut.Validate(new CreateAdsChannelInputDto("Google Ads", true));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithEmptyName_HasErrorOnName()
    {
        var result = _sut.Validate(new CreateAdsChannelInputDto("", true));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_WithNullName_HasErrorOnName()
    {
        var result = _sut.Validate(new CreateAdsChannelInputDto(null, true));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_WithNameOver100Characters_HasErrorOnName()
    {
        var name = new string('a', 101);

        var result = _sut.Validate(new CreateAdsChannelInputDto(name, true));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_WithNameAtMaxLength_ReturnsValid()
    {
        var name = new string('a', 100);

        var result = _sut.Validate(new CreateAdsChannelInputDto(name, true));

        result.IsValid.ShouldBeTrue();
    }
}
