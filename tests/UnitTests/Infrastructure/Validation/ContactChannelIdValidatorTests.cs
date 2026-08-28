using ContactChannel.Application.Dtos;
using Infrastructure.Validation.FluentValidation.ContactChannel;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Validation;

public sealed class ContactChannelIdValidatorTests
{
    private readonly ContactChannelIdValidator _sut = new();

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(int.MaxValue)]
    public void Validate_WithAPositiveIdentifier_ReturnsValid(int id)
    {
        _sut.Validate(new ContactChannelIdInputDto(id)).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Validate_WithANonPositiveIdentifier_HasTheErrorOnId(int id)
    {
        var result = _sut.Validate(new ContactChannelIdInputDto(id));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.PropertyName == nameof(ContactChannelIdInputDto.Id)
            && e.ErrorMessage == "The contact channel identifier must be greater than zero.");
    }
}
