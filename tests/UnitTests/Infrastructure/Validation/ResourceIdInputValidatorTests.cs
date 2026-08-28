using Shared.Application.Dtos;
using Infrastructure.Validation.FluentValidation.Shared;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Validation;

public sealed class ResourceIdInputValidatorTests
{
    private readonly ResourceIdInputValidator _sut = new();

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(int.MaxValue)]
    public void Validate_WithAPositiveIdentifier_ReturnsValid(int id)
    {
        _sut.Validate(new ResourceIdInputDto(id)).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Validate_WithANonPositiveIdentifier_HasTheErrorOnId(int id)
    {
        var result = _sut.Validate(new ResourceIdInputDto(id));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.PropertyName == nameof(ResourceIdInputDto.Id)
            && e.ErrorMessage == "The identifier must be greater than zero.");
    }
}
