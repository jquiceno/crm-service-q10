using BusinessStatus.Domain.Errors;
using BusinessStatus.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.BusinessStatus.Domain;

public sealed class StatusColorTests
{
    [Theory]
    [InlineData("49ff7c")]
    [InlineData("49FF7C")]
    [InlineData("49Ff7C")]
    [InlineData("000000")]
    [InlineData("ffffff")]
    public void Create_WithSixHexadecimalCharacters_ReturnsValueObject(string value)
    {
        var result = StatusColor.Create(value);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(value);
    }

    [Theory]
    [InlineData("#49ff7c")]
    [InlineData("49ff7")]
    [InlineData("49ff7cc")]
    [InlineData("zzzzzz")]
    [InlineData("49 f7c")]
    public void Create_WithMalformedValue_ReturnsInvalidColorFormat(string value)
    {
        var result = StatusColor.Create(value);

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(BusinessStatusErrors.InvalidColorFormat with { Value = value });
        result.TypedError.Property.ShouldBe("Color");
    }

    /// <summary>
    /// Absence of colour is never a value object: the aggregate resolves a null or empty colour as
    /// "no colour" and never reaches this factory, so calling it with nothing is a format failure.
    /// The absence path itself is covered by <see cref="BusinessStatusAggregateTests"/>.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Create_WithoutValue_ReturnsInvalidColorFormat(string? value)
    {
        var result = StatusColor.Create(value);

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(BusinessStatusErrors.InvalidColorFormat with { Value = value });
    }

    [Fact]
    public void Create_KeepsTheCasingWrittenByTheUser()
    {
        var lowercase = StatusColor.Create("49ff7c");
        var uppercase = StatusColor.Create("49FF7C");

        lowercase.Value.Value.ShouldBe("49ff7c");
        uppercase.Value.Value.ShouldBe("49FF7C");
    }

    [Fact]
    public void Equals_WithTheSameValue_IsTrue()
    {
        var first = StatusColor.Create("49ff7c").Value;
        var second = StatusColor.Create("49ff7c").Value;
        var other = StatusColor.Create("49FF7C").Value;

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.ShouldNotBe(other);
    }
}
