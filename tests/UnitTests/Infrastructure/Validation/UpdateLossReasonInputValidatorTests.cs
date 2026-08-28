using Infrastructure.Adapters.Validation;
using Infrastructure.Validation.FluentValidation.LossReasons;
using LossReason.Application.UseCases.UpdateLossReason;
using LossReason.Domain.Aggregates;
using LossReason.Domain.Errors;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Validation;

public sealed class UpdateLossReasonInputValidatorTests
{
    private const string ValidName = "Precio";

    private static string NameOfMaxLength => new('a', LossReasonAggregate.NameMaxLength);

    private static string NameLongerThanMax => new('a', LossReasonAggregate.NameMaxLength + 1);

    private readonly UpdateLossReasonInputValidator _sut = new();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_WithValidInput_ReturnsValid(bool isActive)
    {
        var result = _sut.Validate(new UpdateLossReasonInputDto(ValidName, isActive));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithNameOfMaxLength_ReturnsValid()
    {
        var result = _sut.Validate(new UpdateLossReasonInputDto(NameOfMaxLength, IsActive: true));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithBlankName_ReportsTheDomainNameRequired(string? name)
    {
        var result = _sut.Validate(new UpdateLossReasonInputDto(name, IsActive: true));

        result.IsValid.ShouldBeFalse();
        var failure = result.Errors.Single(e => e.PropertyName == nameof(UpdateLossReasonInputDto.Name));
        failure.ErrorMessage.ShouldBe(LossReasonErrors.NameRequired.Message);
        failure.CustomState.ShouldBe(LossReasonErrors.NameRequired);
    }

    [Fact]
    public void Validate_WithNameLongerThanMax_KeepsTheMaxAttributeFromTheDomainError()
    {
        var result = _sut.Validate(new UpdateLossReasonInputDto(NameLongerThanMax, IsActive: true));

        result.IsValid.ShouldBeFalse();
        var failure = result.Errors.Single(e => e.PropertyName == nameof(UpdateLossReasonInputDto.Name));
        failure.ErrorMessage.ShouldBe(LossReasonErrors.NameTooLong.Message);

        var state = failure.CustomState.ShouldBeOfType<ValidationError>();
        state.Attributes.ShouldNotBeNull();
        state.Attributes!["max"].ShouldBe(LossReasonAggregate.NameMaxLength);
    }

    [Fact]
    public void Validate_WithNoRuleOnIsActive_AcceptsBothStates()
    {
        // IsActive is non-nullable on the update DTO (unlike create), so there is nothing to
        // validate: an explicit null is rejected by the deserializer before the validator runs.
        _sut.Validate(new UpdateLossReasonInputDto(ValidName, IsActive: false)).IsValid.ShouldBeTrue();
        _sut.Validate(new UpdateLossReasonInputDto(ValidName, IsActive: true)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_ThroughTheAdapter_CarriesTheDomainAttributesIntoTheResult()
    {
        var adapter = new FluentRequestValidationAdapter<UpdateLossReasonInputDto>(_sut);

        var result = await adapter.ValidateAsync(new UpdateLossReasonInputDto(NameLongerThanMax, IsActive: true));

        result.IsFailure.ShouldBeTrue();
        var name = result.Error.Details.Single(d => d.Property == nameof(UpdateLossReasonInputDto.Name));
        name.Errors.ShouldNotBeNull();
        name.Errors!.ShouldContain(LossReasonErrors.NameTooLong.Message);
        name.Attributes.ShouldNotBeNull();
        name.Attributes!["max"].ShouldBe(LossReasonAggregate.NameMaxLength);
    }
}
