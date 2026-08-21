using Activities.Domain.Errors;
using Activities.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.Activities.Domain.ValueObjects;

public sealed class OutcomeTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingText_ReturnsOutcomeRequired(string? value)
    {
        var result = Outcome.Create(value);

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(ActivityErrors.OutcomeRequired);
    }

    [Fact]
    public void Create_HasNoLengthCap()
    {
        // DEC-3: the logical contract of the column is varchar(MAX); the 2000-character limit of
        // the divergent tenants is enforced at the API edge, not here.
        var result = Outcome.Create(new string('x', 5000));

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_WithValidText_KeepsIt()
    {
        var result = Outcome.Create("the applicant answered");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe("the applicant answered");
    }

    [Fact]
    public void Equality_IsByValue()
    {
        Outcome.Create("a").Value.ShouldBe(Outcome.Create("a").Value);
        Outcome.Create("a").Value.ShouldNotBe(Outcome.Create("b").Value);
    }
}
