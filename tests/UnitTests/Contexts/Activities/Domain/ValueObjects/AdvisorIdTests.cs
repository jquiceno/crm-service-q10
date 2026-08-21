using Activities.Domain;
using Activities.Domain.Errors;
using Activities.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.Activities.Domain.ValueObjects;

public sealed class AdvisorIdTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingCode_ReturnsAdvisorIdRequired(string? value)
    {
        var result = AdvisorId.Create(value);

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(ActivityErrors.AdvisorIdRequired);
    }

    [Fact]
    public void Create_BeyondTheColumnLimit_ReturnsAdvisorIdTooLong()
    {
        var result = AdvisorId.Create(new string('9', ActivityLimits.AdvisorIdMaxLength + 1));

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(ActivityErrors.AdvisorIdTooLong);
    }

    [Fact]
    public void Create_AtTheColumnLimit_Succeeds()
    {
        var result = AdvisorId.Create(new string('9', ActivityLimits.AdvisorIdMaxLength));

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_WithValidCode_KeepsIt()
    {
        var result = AdvisorId.Create("339968541842");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe("339968541842");
    }

    [Fact]
    public void Equality_IsByValue()
    {
        AdvisorId.Create("1").Value.ShouldBe(AdvisorId.Create("1").Value);
        AdvisorId.Create("1").Value.ShouldNotBe(AdvisorId.Create("2").Value);
    }
}
