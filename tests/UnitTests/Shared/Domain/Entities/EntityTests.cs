using System.Diagnostics.CodeAnalysis;
using Shared.Domain.Entities;
using Shouldly;
using Xunit;

namespace UnitTests.Shared.Domain.Entities;

public sealed class EntityTests
{
    private sealed class TestEntity : Entity<int>
    {
        public TestEntity(int id) => Id = id;
    }

    private sealed class OtherTestEntity : Entity<int>
    {
        public OtherTestEntity(int id) => Id = id;
    }

    private sealed class StringIdEntity : Entity<string>
    {
        public StringIdEntity() { }
        public StringIdEntity(string id) => Id = id;
    }

    [Fact]
    public void Equals_WithSameId_ReturnsTrue()
    {
        var left = new TestEntity(1);
        var right = new TestEntity(1);

        left.Equals(right).ShouldBeTrue();
    }

    [Fact]
    public void Equals_WithDifferentId_ReturnsFalse()
    {
        var left = new TestEntity(1);
        var right = new TestEntity(2);

        left.Equals(right).ShouldBeFalse();
    }

    [Fact]
    [SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code", Justification = "Equality-contract test: the constant outcome is the behavior under test.")]
    public void Equals_WithNull_ReturnsFalse()
    {
        var entity = new TestEntity(1);

        entity.Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void Equals_WithNonEntityObject_ReturnsFalse()
    {
        var entity = new TestEntity(1);

        entity.Equals("not an entity").ShouldBeFalse();
    }

    [Fact]
    public void Equals_WithDifferentEntitySubclassSameId_ReturnsTrue()
    {
        Entity<int> left = new TestEntity(1);
        Entity<int> right = new OtherTestEntity(1);

        left.Equals(right).ShouldBeTrue();
    }

    [Fact]
    public void GetHashCode_WithSameId_ReturnsSameValue()
    {
        var left = new TestEntity(1);
        var right = new TestEntity(1);

        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void GetHashCode_WithNullId_ReturnsZero()
    {
        var entity = new StringIdEntity();

        entity.GetHashCode().ShouldBe(0);
    }

    [Fact]
    [SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code", Justification = "Equality-contract test: the constant outcome is the behavior under test.")]
    public void EqualityOperator_WithBothNull_ReturnsTrue()
    {
        Entity<int>? left = null;
        Entity<int>? right = null;

        (left == right).ShouldBeTrue();
    }

    [Fact]
    [SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code", Justification = "Equality-contract test: the constant outcome is the behavior under test.")]
    public void EqualityOperator_WithOneNull_ReturnsFalse()
    {
        Entity<int>? left = new TestEntity(1);
        Entity<int>? right = null;

        (left == right).ShouldBeFalse();
    }

    [Fact]
    public void InequalityOperator_WithDifferentId_ReturnsTrue()
    {
        var left = new TestEntity(1);
        var right = new TestEntity(2);

        (left != right).ShouldBeTrue();
    }
}
