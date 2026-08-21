using ContactChannel.Application.UseCases.GetContactChannels;
using Infrastructure.Validation.FluentValidation.ContactChannel;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Validation;

public sealed class GetContactChannelsValidatorTests
{
    private readonly GetContactChannelsValidator _sut = new();

    private static GetContactChannelsInputDto WithSearchName(string? searchName) =>
        new(IsActive: null, SearchName: searchName);

    [Fact]
    public void Validate_WithoutFilters_ReturnsValid()
    {
        var result = _sut.Validate(WithSearchName(null));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithSearchNameAtMaxLength_ReturnsValid()
    {
        var result = _sut.Validate(
            WithSearchName(new string('a', GetContactChannelsValidator.SearchNameMaxLength)));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithSearchNameOverMaxLength_HasErrorOnSearchName()
    {
        var result = _sut.Validate(
            WithSearchName(new string('a', GetContactChannelsValidator.SearchNameMaxLength + 1)));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.PropertyName == "SearchName"
            && e.ErrorMessage ==
                $"Search name must not exceed {GetContactChannelsValidator.SearchNameMaxLength} characters.");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void Validate_AcceptsEveryStateFilter(bool? isActive)
    {
        var result = _sut.Validate(new GetContactChannelsInputDto(isActive, SearchName: null));

        result.IsValid.ShouldBeTrue();
    }
}
