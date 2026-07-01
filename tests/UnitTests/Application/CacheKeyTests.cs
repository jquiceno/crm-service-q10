using Shared.Application.Caching;
using Shouldly;
using Xunit;

namespace UnitTests.Application;

public sealed class CacheKeyTests
{
    [Fact]
    public void Resource_BuildsCanonicalKey() =>
        CacheKey.For("orders").Resource("order", 42)
            .ShouldBe("ctx:orders:v1:order:42");

    [Fact]
    public void Tenant_InsertsPartitionSegment() =>
        CacheKey.For("orders").Tenant("acme").Resource("order", 42)
            .ShouldBe("ctx:orders:v1:t:acme:order:42");

    [Fact]
    public void Prefix_BuildsFamilyKey() =>
        CacheKey.For("orders").Prefix("order:list")
            .ShouldBe("ctx:orders:v1:order:list");

    [Fact]
    public void Tenant_Prefix_Combined() =>
        CacheKey.For("orders").Tenant("acme").Prefix("order:list")
            .ShouldBe("ctx:orders:v1:t:acme:order:list");

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("has:colon")]
    public void For_RejectsInvalidContext(string context) =>
        Should.Throw<ArgumentException>(() => CacheKey.For(context));

    [Fact]
    public void Tenant_RejectsColon() =>
        Should.Throw<ArgumentException>(() => CacheKey.For("orders").Tenant("a:b"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Prefix_RejectsEmptyOrWhitespace(string prefix) =>
        Should.Throw<ArgumentException>(() => CacheKey.For("orders").Prefix(prefix));
}
