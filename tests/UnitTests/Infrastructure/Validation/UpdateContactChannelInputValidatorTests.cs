using ContactChannel.Application.UseCases.UpdateContactChannel;
using ContactChannel.Domain.Aggregates;
using ContactChannel.Domain.Errors;
using Infrastructure.Validation.FluentValidation.ContactChannel;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Validation;

public sealed class UpdateContactChannelInputValidatorTests
{
    private readonly UpdateContactChannelInputValidator _sut = new();

    private static UpdateContactChannelInputDto WithName(string? name) => new(name, IsActive: true);

    [Fact]
    public void Validate_WithANameAndAState_ReturnsValid()
    {
        _sut.Validate(WithName("WhatsApp")).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithoutAUsableName_HasTheRequiredErrorOnName(string? name)
    {
        var result = _sut.Validate(WithName(name));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.PropertyName == nameof(UpdateContactChannelInputDto.Name)
            && e.ErrorMessage == ContactChannelErrors.NameRequired.Message);
    }

    [Fact]
    public void Validate_WithNameAtMaxLength_ReturnsValid()
    {
        _sut.Validate(WithName(new string('a', ContactChannelAggregate.NameMaxLength))).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithNameOverMaxLength_CarriesTheDomainErrorAsState()
    {
        var result = _sut.Validate(WithName(new string('a', ContactChannelAggregate.NameMaxLength + 1)));

        result.IsValid.ShouldBeFalse();
        var failure = result.Errors.Single(e => e.PropertyName == nameof(UpdateContactChannelInputDto.Name));
        failure.ErrorMessage.ShouldBe(ContactChannelErrors.NameTooLong.Message);
        var state = failure.CustomState.ShouldBeOfType<ValidationError>();
        state.Attributes.ShouldNotBeNull();
        state.Attributes["maxLength"].ShouldBe(ContactChannelAggregate.NameMaxLength);
    }

    [Fact]
    public void Validate_WithoutState_HasTheRequiredErrorOnIsActive()
    {
        var result = _sut.Validate(new UpdateContactChannelInputDto("WhatsApp", IsActive: null));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.PropertyName == nameof(UpdateContactChannelInputDto.IsActive)
            && e.ErrorMessage == ContactChannelErrors.IsActiveRequired.Message);
    }

    [Fact]
    public void Validate_CarriesTheDomainErrorAsStateOnEveryRule()
    {
        var result = _sut.Validate(new UpdateContactChannelInputDto(null, IsActive: null));

        result.Errors.ShouldAllBe(e => e.CustomState is ValidationError);
    }
}
