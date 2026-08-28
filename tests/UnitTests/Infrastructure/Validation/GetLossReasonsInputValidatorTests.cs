using Infrastructure.Adapters.Validation;
using Infrastructure.Validation.FluentValidation.LossReasons;
using LossReason.Application.UseCases.GetLossReasons;
using LossReason.Domain.Aggregates;
using LossReason.Domain.Errors;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Validation;

public sealed class GetLossReasonsInputValidatorTests
{
    private static string NameOfMaxLength => new('a', LossReasonAggregate.NameMaxLength);

    private static string NameLongerThanMax => new('a', LossReasonAggregate.NameMaxLength + 1);

    private readonly GetLossReasonsInputValidator _sut = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Precio")]
    public void Validate_WithAnyNameUpToTheLimit_ReturnsValid(string? name)
    {
        var result = _sut.Validate(new GetLossReasonsInputDto(name, IsActive: null));

        // A blank name means "do not filter by name" on the listing, so unlike create and update
        // there is no NotEmpty rule here: requiring it would turn the unfiltered listing into a 400.
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_WithAnyIsActive_ReturnsValid(bool? isActive)
    {
        var result = _sut.Validate(new GetLossReasonsInputDto("Precio", isActive));

        // D9: the state filter is optional; null means every loss reason.
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithNameOfMaxLength_ReturnsValid()
    {
        var result = _sut.Validate(new GetLossReasonsInputDto(NameOfMaxLength, IsActive: null));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithNameLongerThanMax_KeepsTheMaxAttributeFromTheDomainError()
    {
        var result = _sut.Validate(new GetLossReasonsInputDto(NameLongerThanMax, IsActive: null));

        result.IsValid.ShouldBeFalse();
        var failure = result.Errors.Single(e => e.PropertyName == nameof(GetLossReasonsInputDto.Name));
        failure.ErrorMessage.ShouldBe(LossReasonErrors.NameTooLong.Message);

        var state = failure.CustomState.ShouldBeOfType<ValidationError>();
        state.Attributes.ShouldNotBeNull();
        state.Attributes!["max"].ShouldBe(LossReasonAggregate.NameMaxLength);
    }

    [Fact]
    public async Task ValidateAsync_ThroughTheAdapter_CarriesTheDomainAttributesIntoTheResult()
    {
        var adapter = new FluentRequestValidationAdapter<GetLossReasonsInputDto>(_sut);

        var result = await adapter.ValidateAsync(new GetLossReasonsInputDto(NameLongerThanMax, IsActive: null));

        result.IsFailure.ShouldBeTrue();
        var name = result.Error.Details.Single(d => d.Property == nameof(GetLossReasonsInputDto.Name));
        name.Errors.ShouldNotBeNull();
        name.Errors!.ShouldContain(LossReasonErrors.NameTooLong.Message);
        name.Attributes.ShouldNotBeNull();
        name.Attributes!["max"].ShouldBe(LossReasonAggregate.NameMaxLength);
    }
}
