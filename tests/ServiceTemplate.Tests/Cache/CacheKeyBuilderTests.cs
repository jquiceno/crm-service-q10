using FluentAssertions;
using Infrastructure.Cache;
using Xunit;

namespace ServiceTemplate.Tests.Cache;

public sealed class CacheKeyBuilderTests
{
    [Fact]
    public void Build_WithAllParts_FormatsKeyCorrectly()
    {
        var key = CacheKeyBuilder.Build("api:v1", "weather-forecasts", null, "acme", "en-US");

        // Format: {prefix}:{route}:{queryHash}:{tenant}:{locale}
        key.Should().StartWith("api:v1:weather-forecasts:");
        key.Should().EndWith(":acme:en-us");
    }

    [Fact]
    public void Build_WithNullQueryString_UsesUnderscore()
    {
        var key = CacheKeyBuilder.Build("api:v1", "route", null, null, null);

        var parts = key.Split(':');
        // api:v1:route:{queryHash}:{tenant}:{locale}
        // But the path "route" has no colons, so parts[3] is the query hash
        parts[3].Should().Be("_", "null query string should produce underscore hash");
    }

    [Fact]
    public void Build_WithEmptyQueryString_UsesUnderscore()
    {
        var key = CacheKeyBuilder.Build("api:v1", "route", string.Empty, null, null);

        key.Should().Contain(":_:", "empty query string should produce underscore hash");
    }

    [Fact]
    public void Build_WithQueryString_ProducesHexHash()
    {
        var key = CacheKeyBuilder.Build("api:v1", "route", "?page=1&size=10", null, null);

        var parts = key.Split(':');
        var queryHash = parts[3];

        queryHash.Should().HaveLength(16, "hash is 8 bytes → 16 hex chars");
        queryHash.Should().MatchRegex("^[0-9a-f]+$", "should be lowercase hex");
    }

    [Fact]
    public void Build_SameQueryParamsDifferentOrder_ProduceSameKey()
    {
        var key1 = CacheKeyBuilder.Build("api:v1", "route", "?a=1&b=2", null, null);
        var key2 = CacheKeyBuilder.Build("api:v1", "route", "?b=2&a=1", null, null);

        key1.Should().Be(key2, "query param order should not affect the cache key");
    }

    [Fact]
    public void Build_DifferentQueryStrings_ProduceDifferentKeys()
    {
        var key1 = CacheKeyBuilder.Build("api:v1", "route", "?page=1", null, null);
        var key2 = CacheKeyBuilder.Build("api:v1", "route", "?page=2", null, null);

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void Build_WithNullTenant_UsesFallback()
    {
        var key = CacheKeyBuilder.Build("api:v1", "route", null, null, null);

        key.Should().Contain(":_:_", "null tenant and locale should both fall back to underscore");
    }

    [Fact]
    public void Build_WithWhitespaceTenant_UsesFallback()
    {
        var key = CacheKeyBuilder.Build("api:v1", "route", null, "   ", null);

        // tenant part (second-to-last segment) should be underscore
        key.Should().EndWith(":_:_");
    }

    [Fact]
    public void Build_TenantIsCaseNormalized()
    {
        var lower = CacheKeyBuilder.Build("api:v1", "route", null, "ACME", null);
        var upper = CacheKeyBuilder.Build("api:v1", "route", null, "acme", null);

        lower.Should().Be(upper, "tenant is normalized to lowercase");
    }

    [Fact]
    public void Build_DifferentTenants_ProduceDifferentKeys()
    {
        var keyA = CacheKeyBuilder.Build("api:v1", "route", null, "tenant-a", null);
        var keyB = CacheKeyBuilder.Build("api:v1", "route", null, "tenant-b", null);

        keyA.Should().NotBe(keyB);
    }

    [Fact]
    public void Build_DifferentLocales_ProduceDifferentKeys()
    {
        var keyEn = CacheKeyBuilder.Build("api:v1", "route", null, null, "en-US");
        var keyEs = CacheKeyBuilder.Build("api:v1", "route", null, null, "es-CO");

        keyEn.Should().NotBe(keyEs);
    }

    [Fact]
    public void BuildRoutePrefix_AppendsTrailingColon()
    {
        var prefix = CacheKeyBuilder.BuildRoutePrefix("api:v1", "weather-forecasts");

        prefix.Should().Be("api:v1:weather-forecasts:");
    }

    [Fact]
    public void BuildRoutePrefix_MatchesKeysBuiltForSameRoute()
    {
        var routePrefix = CacheKeyBuilder.BuildRoutePrefix("api:v1", "orders");
        var key = CacheKeyBuilder.Build("api:v1", "orders", "?page=1", "acme", "en-US");

        key.Should().StartWith(routePrefix, "the prefix should match all keys for the same route");
    }
}
