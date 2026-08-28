using Infrastructure.Validation.FluentValidation.Shared;
using Shared.Application.Dtos;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Validation;

public sealed class PageQueryInputValidatorTests
{
    private readonly PageQueryInputValidator _sut = new();

    [Fact]
    public void Validate_WithDefaultValues_ReturnsValid()
    {
        var result = _sut.Validate(new PageQueryInputDto());

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithNegativePageIndex_HasErrorOnPageIndex()
    {
        var result = _sut.Validate(new PageQueryInputDto(PageIndex: -1));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.PropertyName == "PageIndex" && e.ErrorMessage == "Page index must be greater than or equal to 0.");
    }

    [Fact]
    public void Validate_WithZeroPageIndex_ReturnsValid()
    {
        var result = _sut.Validate(new PageQueryInputDto(PageIndex: 0));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithPageSizeBelowRange_HasErrorOnPageSize()
    {
        var result = _sut.Validate(new PageQueryInputDto(PageSize: 0));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.PropertyName == "PageSize"
            && e.ErrorMessage == $"Page size must be between 1 and {PageQueryInputDto.MaxPageSize}.");
    }

    [Fact]
    public void Validate_WithPageSizeAboveRange_HasErrorOnPageSize()
    {
        var result = _sut.Validate(new PageQueryInputDto(PageSize: PageQueryInputDto.MaxPageSize + 1));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "PageSize");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(PageQueryInputDto.MaxPageSize)]
    public void Validate_WithPageSizeAtBoundaries_ReturnsValid(int pageSize)
    {
        var result = _sut.Validate(new PageQueryInputDto(PageSize: pageSize));

        result.IsValid.ShouldBeTrue();
    }
}
