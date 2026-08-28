using ContactChannel.Application.UseCases.CreateContactChannel;
using ContactChannel.Domain.Aggregates;
using ContactChannel.Domain.Errors;
using Shared.Results.Errors;
using Infrastructure.Validation.FluentValidation.ContactChannel;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Validation;

public sealed class CreateContactChannelValidatorTests
{
    private readonly CreateContactChannelValidator _sut = new();

    private static CreateContactChannelInputDto WithName(string? name) => new(name, IsActive: true);

    [Fact]
    public void Validate_WithoutState_HasTheRequiredErrorOnIsActive()
    {
        var result = _sut.Validate(new CreateContactChannelInputDto("WhatsApp", IsActive: null));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.PropertyName == nameof(CreateContactChannelInputDto.IsActive)
            && e.ErrorMessage == "The contact channel state is required.");
    }

    [Fact]
    public void Validate_WithAName_ReturnsValid()
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
            e.PropertyName == nameof(CreateContactChannelInputDto.Name)
            && e.ErrorMessage == ContactChannelErrors.NameRequired.Message);
    }

    [Fact]
    public void Validate_WithNameAtMaxLength_ReturnsValid()
    {
        var result = _sut.Validate(WithName(new string('a', ContactChannelAggregate.NameMaxLength)));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithNameOverMaxLength_HasTheLengthErrorOnName()
    {
        var result = _sut.Validate(WithName(new string('a', ContactChannelAggregate.NameMaxLength + 1)));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.PropertyName == nameof(CreateContactChannelInputDto.Name)
            && e.ErrorMessage == ContactChannelErrors.NameTooLong.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_AcceptsEitherState(bool isActive)
    {
        _sut.Validate(new CreateContactChannelInputDto("WhatsApp", isActive)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithNameOverMaxLength_CarriesTheDomainErrorAsState()
    {
        var result = _sut.Validate(WithName(new string('a', ContactChannelAggregate.NameMaxLength + 1)));

        var failure = result.Errors.Single(e => e.PropertyName == nameof(CreateContactChannelInputDto.Name));
        var state = failure.CustomState.ShouldBeOfType<ValidationError>();
        state.Attributes.ShouldNotBeNull();
        state.Attributes["maxLength"].ShouldBe(ContactChannelAggregate.NameMaxLength);
    }

    [Fact]
    public void Validate_CarriesTheDomainErrorAsStateOnEveryRule()
    {
        var result = _sut.Validate(new CreateContactChannelInputDto(null, IsActive: null));

        result.Errors.ShouldAllBe(e => e.CustomState is ValidationError);
    }
}
