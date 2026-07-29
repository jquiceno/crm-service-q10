using System.Diagnostics.CodeAnalysis;
using Shared.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace UnitTests.Shared.Domain.ValueObjects;

public sealed class ValueObjectTests
{
    private sealed class Money(decimal amount, string? currency) : ValueObject
    {
        public decimal Amount { get; } = amount;
        public string? Currency { get; } = currency;

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Amount;
            yield return Currency;
        }
    }

    private sealed class OtherValueObject(decimal amount) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return amount;
        }
    }

    [Fact]
    public void Equals_WithSameComponents_ReturnsTrue()
    {
        var left = new Money(10m, "USD");
        var right = new Money(10m, "USD");

        left.Equals(right).ShouldBeTrue();
    }

    [Fact]
    public void Equals_WithDifferentComponents_ReturnsFalse()
    {
        var left = new Money(10m, "USD");
        var right = new Money(20m, "USD");

        left.Equals(right).ShouldBeFalse();
    }

    [Fact]
    [SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code", Justification = "Equality-contract test: the constant outcome is the behavior under test.")]
    public void Equals_WithNull_ReturnsFalse()
    {
        var left = new Money(10m, "USD");

        left.Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void Equals_WithNonValueObject_ReturnsFalse()
    {
        var left = new Money(10m, "USD");

        left.Equals("not a value object").ShouldBeFalse();
    }

    [Fact]
    public void Equals_WithDifferentConcreteType_ReturnsFalse()
    {
        ValueObject left = new Money(10m, "USD");
        ValueObject right = new OtherValueObject(10m);

        left.Equals(right).ShouldBeFalse();
    }

    [Fact]
    public void GetHashCode_WithSameComponents_ReturnsSameValue()
    {
        var left = new Money(10m, "USD");
        var right = new Money(10m, "USD");

        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void GetHashCode_WithNullComponent_DoesNotThrow()
    {
        var valueObject = new Money(10m, null);

        Should.NotThrow(() => valueObject.GetHashCode());
    }

    [Fact]
    [SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code", Justification = "Equality-contract test: the constant outcome is the behavior under test.")]
    public void EqualityOperator_WithBothNull_ReturnsTrue()
    {
        ValueObject? left = null;
        ValueObject? right = null;

        (left == right).ShouldBeTrue();
    }

    [Fact]
    [SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code", Justification = "Equality-contract test: the constant outcome is the behavior under test.")]
    public void EqualityOperator_WithOneNull_ReturnsFalse()
    {
        ValueObject? left = new Money(10m, "USD");
        ValueObject? right = null;

        (left == right).ShouldBeFalse();
    }

    [Fact]
    public void InequalityOperator_WithDifferentComponents_ReturnsTrue()
    {
        var left = new Money(10m, "USD");
        var right = new Money(20m, "USD");

        (left != right).ShouldBeTrue();
    }

    [Fact]
    public void InequalityOperator_WithSameComponents_ReturnsFalse()
    {
        var left = new Money(10m, "USD");
        var right = new Money(10m, "USD");

        (left != right).ShouldBeFalse();
    }
}
