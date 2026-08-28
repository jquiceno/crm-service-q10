using Infrastructure.Validation.FluentValidation.LossReasons;
using LossReason.Application.UseCases.CreateLossReason;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Validation;

public sealed class CreateLossReasonInputValidatorTests
{
    private const string ValidName = "Precio";
    private const string IsActiveRequiredMessage = "Whether the loss reason is active is required.";

    private readonly CreateLossReasonInputValidator _sut = new();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_WithIsActiveProvided_ReturnsValid(bool isActive)
    {
        var result = _sut.Validate(new CreateLossReasonInputDto(ValidName, isActive));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithNullIsActive_HasErrorOnIsActive()
    {
        var result = _sut.Validate(new CreateLossReasonInputDto(ValidName, IsActive: null));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.PropertyName == nameof(CreateLossReasonInputDto.IsActive)
            && e.ErrorMessage == IsActiveRequiredMessage);
    }

    [Fact]
    public void Validate_WithInvalidName_DoesNotReportIt()
    {
        // Name is the aggregate's invariant, not a structural rule -- see the validator's comment.
        var result = _sut.Validate(new CreateLossReasonInputDto(Name: null, IsActive: true));

        result.IsValid.ShouldBeTrue();
    }
}
