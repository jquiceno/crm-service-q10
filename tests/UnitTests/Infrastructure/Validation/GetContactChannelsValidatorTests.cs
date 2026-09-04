using ContactChannel.Application.UseCases.GetContactChannels;
using Infrastructure.Validation.FluentValidation.ContactChannel;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Validation;

public sealed class GetContactChannelsValidatorTests
{
    private readonly GetContactChannelsValidator _sut = new();

    private static GetContactChannelsInputDto WithSearch(string? search) =>
        new(IsActive: null, Search: search);

    [Fact]
    public void Validate_WithoutFilters_ReturnsValid()
    {
        var result = _sut.Validate(WithSearch(null));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithSearchAtMaxLength_ReturnsValid()
    {
        var result = _sut.Validate(
            WithSearch(new string('a', GetContactChannelsValidator.SearchMaxLength)));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithSearchOverMaxLength_HasErrorOnSearch()
    {
        var result = _sut.Validate(
            WithSearch(new string('a', GetContactChannelsValidator.SearchMaxLength + 1)));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.PropertyName == nameof(GetContactChannelsInputDto.Search)
            && e.ErrorMessage ==
                $"Search must not exceed {GetContactChannelsValidator.SearchMaxLength} characters.");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void Validate_AcceptsEveryStateFilter(bool? isActive)
    {
        var result = _sut.Validate(new GetContactChannelsInputDto(isActive, Search: null));

        result.IsValid.ShouldBeTrue();
    }
}
