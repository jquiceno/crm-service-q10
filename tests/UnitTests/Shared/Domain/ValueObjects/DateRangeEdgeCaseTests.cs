using Shared.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace UnitTests.Shared.Domain.ValueObjects;

/// <summary>
/// Covers the value-equality members of <see cref="DateRange"/> (inherited from
/// <see cref="ValueObject"/>), which <see cref="DateRangeTests"/> does not exercise.
/// </summary>
public sealed class DateRangeEdgeCaseTests
{
    [Fact]
    public void Equals_WithSameStartAndEnd_ReturnsTrue()
    {
        var start = new DateTime(2030, 1, 1);
        var end = new DateTime(2030, 1, 31);
        var left = DateRange.Create(start, end).Value;
        var right = DateRange.Create(start, end).Value;

        left.Equals(right).ShouldBeTrue();
        (left == right).ShouldBeTrue();
    }

    [Fact]
    public void Equals_WithDifferentEnd_ReturnsFalse()
    {
        var start = new DateTime(2030, 1, 1);
        var left = DateRange.Create(start, new DateTime(2030, 1, 31)).Value;
        var right = DateRange.Create(start, new DateTime(2030, 2, 1)).Value;

        left.Equals(right).ShouldBeFalse();
        (left != right).ShouldBeTrue();
    }

    [Fact]
    public void Equals_WhenOnlyOneIsOpenEnded_ReturnsFalse()
    {
        var left = DateRange.Create(null, null).Value;
        var right = DateRange.Create(new DateTime(2030, 1, 1), new DateTime(2030, 1, 31)).Value;

        left.Equals(right).ShouldBeFalse();
    }

    [Fact]
    public void GetHashCode_WithSameStartAndEnd_ReturnsSameValue()
    {
        var left = DateRange.Create(null, null).Value;
        var right = DateRange.Create(null, null).Value;

        left.GetHashCode().ShouldBe(right.GetHashCode());
    }
}
