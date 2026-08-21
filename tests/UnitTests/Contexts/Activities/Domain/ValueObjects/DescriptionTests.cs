using Activities.Domain;
using Activities.Domain.Errors;
using Activities.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.Activities.Domain.ValueObjects;

public sealed class DescriptionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingText_ReturnsDescriptionRequired(string? value)
    {
        var result = Description.Create(value);

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(ActivityErrors.DescriptionRequired);
    }

    [Fact]
    public void Create_BeyondTheColumnLimit_ReturnsDescriptionTooLong()
    {
        var result = Description.Create(new string('x', ActivityLimits.DescriptionMaxLength + 1));

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(ActivityErrors.DescriptionTooLong);
    }

    [Fact]
    public void Create_AtTheColumnLimit_Succeeds()
    {
        var result = Description.Create(new string('x', ActivityLimits.DescriptionMaxLength));

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_WithValidText_KeepsIt()
    {
        var result = Description.Create("call the applicant");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe("call the applicant");
    }

    [Fact]
    public void Equality_IsByValue()
    {
        Description.Create("a").Value.ShouldBe(Description.Create("a").Value);
        Description.Create("a").Value.ShouldNotBe(Description.Create("b").Value);
    }
}
