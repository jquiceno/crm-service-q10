using Activities.Domain;
using Activities.Domain.Errors;
using Activities.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.Activities.Domain.ValueObjects;

public sealed class PersonCodeTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingCode_ReturnsPersonCodeRequired(string? value)
    {
        var result = PersonCode.Create(value);

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(ActivityErrors.PersonCodeRequired);
    }

    [Fact]
    public void Create_BeyondTheColumnLimit_ReturnsPersonCodeTooLong()
    {
        var result = PersonCode.Create(new string('9', ActivityLimits.PersonCodeMaxLength + 1));

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(ActivityErrors.PersonCodeTooLong);
    }

    [Fact]
    public void Create_AtTheColumnLimit_Succeeds()
    {
        var result = PersonCode.Create(new string('9', ActivityLimits.PersonCodeMaxLength));

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_WithValidCode_KeepsIt()
    {
        var result = PersonCode.Create("339968541842");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe("339968541842");
    }

    [Fact]
    public void Equality_IsByValue()
    {
        PersonCode.Create("1").Value.ShouldBe(PersonCode.Create("1").Value);
        PersonCode.Create("1").Value.ShouldNotBe(PersonCode.Create("2").Value);
    }
}
