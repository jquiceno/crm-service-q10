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
    private const string ValidName = "Price";

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
    public void Validate_WithNullIsActive_ReportsTheDomainIsActiveRequired()
    {
        var result = _sut.Validate(new UpdateLossReasonInputDto(ValidName, IsActive: null));

        // Required rather than defaulted: an omitted flag would otherwise arrive as false through
        // the CLR default and deactivate the reason without the caller ever asking for it.
        result.IsValid.ShouldBeFalse();
        var failure = result.Errors.Single(e => e.PropertyName == nameof(UpdateLossReasonInputDto.IsActive));
        failure.ErrorMessage.ShouldBe(LossReasonErrors.IsActiveRequired.Message);
        failure.CustomState.ShouldBe(LossReasonErrors.IsActiveRequired);
    }

    [Fact]
    public void Validate_WithBlankNameLongerThanMaxAndNullIsActive_ReportsEveryFailure()
    {
        // Whitespace past the limit violates both name rules at once; no rule short-circuits
        // another, so the response mirrors what the aggregate accumulates. Same shape as create.
        var name = new string(' ', LossReasonAggregate.NameMaxLength + 1);

        var result = _sut.Validate(new UpdateLossReasonInputDto(name, IsActive: null));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage).ShouldBe(
            [
                LossReasonErrors.NameRequired.Message,
                LossReasonErrors.NameTooLong.Message,
                LossReasonErrors.IsActiveRequired.Message
            ],
            ignoreOrder: true);
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
