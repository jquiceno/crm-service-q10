using Infrastructure.Adapters.Validation;
using Infrastructure.Validation.FluentValidation.LossReasons;
using LossReason.Application.UseCases.CreateLossReason;
using LossReason.Domain.Aggregates;
using LossReason.Domain.Errors;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Validation;

public sealed class CreateLossReasonInputValidatorTests
{
    private const string ValidName = "Precio";

    private static string NameOfMaxLength => new('a', LossReasonAggregate.NameMaxLength);

    private static string NameLongerThanMax => new('a', LossReasonAggregate.NameMaxLength + 1);

    private readonly CreateLossReasonInputValidator _sut = new();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_WithValidInput_ReturnsValid(bool isActive)
    {
        var result = _sut.Validate(new CreateLossReasonInputDto(ValidName, isActive));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithNameOfMaxLength_ReturnsValid()
    {
        var result = _sut.Validate(new CreateLossReasonInputDto(NameOfMaxLength, IsActive: true));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithBlankName_ReportsTheDomainNameRequired(string? name)
    {
        var result = _sut.Validate(new CreateLossReasonInputDto(name, IsActive: true));

        result.IsValid.ShouldBeFalse();
        var failure = result.Errors.Single(e => e.PropertyName == nameof(CreateLossReasonInputDto.Name));
        failure.ErrorMessage.ShouldBe(LossReasonErrors.NameRequired.Message);
        failure.CustomState.ShouldBe(LossReasonErrors.NameRequired);
    }

    [Fact]
    public void Validate_WithNameLongerThanMax_KeepsTheMaxAttributeFromTheDomainError()
    {
        var result = _sut.Validate(new CreateLossReasonInputDto(NameLongerThanMax, IsActive: true));

        result.IsValid.ShouldBeFalse();
        var failure = result.Errors.Single(e => e.PropertyName == nameof(CreateLossReasonInputDto.Name));
        failure.ErrorMessage.ShouldBe(LossReasonErrors.NameTooLong.Message);

        // The whole point of WithState: the adapter rebuilds Attributes from here, so the client
        // gets the limit as data instead of having to parse it out of the message.
        var state = failure.CustomState.ShouldBeOfType<ValidationError>();
        state.Attributes.ShouldNotBeNull();
        state.Attributes!["max"].ShouldBe(LossReasonAggregate.NameMaxLength);
    }

    [Fact]
    public void Validate_WithNullIsActive_ReportsTheDomainIsActiveRequired()
    {
        var result = _sut.Validate(new CreateLossReasonInputDto(ValidName, IsActive: null));

        result.IsValid.ShouldBeFalse();
        var failure = result.Errors.Single(e => e.PropertyName == nameof(CreateLossReasonInputDto.IsActive));
        failure.ErrorMessage.ShouldBe(LossReasonErrors.IsActiveRequired.Message);
        failure.CustomState.ShouldBe(LossReasonErrors.IsActiveRequired);
    }

    [Fact]
    public void Validate_WithBlankNameLongerThanMaxAndNullIsActive_ReportsEveryFailure()
    {
        // Whitespace past the limit violates both name rules at once; neither rule short-circuits
        // the other, so the response mirrors what the aggregate accumulates.
        var name = new string(' ', LossReasonAggregate.NameMaxLength + 1);

        var result = _sut.Validate(new CreateLossReasonInputDto(name, IsActive: null));

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
        // End to end: WithState only pays off if FluentRequestValidationAdapter rebuilds the
        // ValidationError from it. Asserting on CustomState alone would not prove that.
        var adapter = new FluentRequestValidationAdapter<CreateLossReasonInputDto>(_sut);

        var result = await adapter.ValidateAsync(new CreateLossReasonInputDto(NameLongerThanMax, IsActive: null));

        result.IsFailure.ShouldBeTrue();

        var name = result.Error.Details.Single(d => d.Property == nameof(CreateLossReasonInputDto.Name));
        name.Errors.ShouldNotBeNull();
        name.Errors!.ShouldContain(LossReasonErrors.NameTooLong.Message);
        name.Attributes.ShouldNotBeNull();
        name.Attributes!["max"].ShouldBe(LossReasonAggregate.NameMaxLength);

        var isActive = result.Error.Details.Single(d => d.Property == nameof(CreateLossReasonInputDto.IsActive));
        isActive.Errors!.ShouldContain(LossReasonErrors.IsActiveRequired.Message);
    }
}
