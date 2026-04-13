using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Infrastructure.Cache;
using Xunit;

namespace ServiceTemplate.Tests.Api;

/// <summary>
/// Verifies the <c>[InvalidateCache]</c> action filter behaviour using the real
/// ASP.NET Core pipeline and an in-memory spy.
/// </summary>
public sealed class InvalidateCacheAttributeTests
{
    // Prefix that InvalidateCacheAttribute("api/v1/weather-forecasts") resolves to.
    private static readonly string ExpectedInvalidationPrefix =
        CacheKeyBuilder.BuildRoutePrefix("api:v1", "api/v1/weather-forecasts");

    // Cache key written by a GET with no tenant / locale headers.
    private static readonly string DefaultGetCacheKey =
        CacheKeyBuilder.Build("api:v1", "api/v1/weather-forecasts", null, null, null);

    [Fact]
    public async Task Create_SuccessfulPost_CallsRemoveByPrefixWithCorrectPrefix()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/weather-forecasts", ValidPayload());

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        factory.CacheService.RemoveByPrefixCalls
            .Should().ContainSingle(p => p == ExpectedInvalidationPrefix,
                "a successful POST must invalidate the route's cache prefix");
    }

    [Fact]
    public async Task Create_ValidationFailure_DoesNotInvalidateCache()
    {
        // TemperatureC = 999 violates the [-90, 60] range → use case returns failure → 400.
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/weather-forecasts", new
        {
            date = "2024-01-15T00:00:00",
            temperatureC = 999,
            summary = "Way too hot"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        factory.CacheService.RemoveByPrefixCalls
            .Should().BeEmpty("a failed POST must not touch the cache");
    }

    [Fact]
    public async Task Create_MissingBody_DoesNotInvalidateCache()
    {
        // Empty body → model binding failure → 400 before action even runs.
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        using var emptyContent = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/weather-forecasts", emptyContent);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        factory.CacheService.RemoveByPrefixCalls
            .Should().BeEmpty("a model-binding failure must not touch the cache");
    }

    [Fact]
    public async Task FullRoundTrip_GetCachesResponse_PostInvalidates_NextGetIsAMiss()
    {
        // This is the primary integration scenario: GET → cache → POST → evict → GET again.
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        // Step 1 — GET: cache miss → handler runs → response stored in cache.
        await client.GetAsync("/api/v1/weather-forecasts");

        factory.CacheService.Contains(DefaultGetCacheKey)
            .Should().BeTrue("GET should populate the cache");

        var setCountAfterGet = factory.CacheService.SetCalls.Count;

        // Step 2 — POST: mutation → cache invalidated.
        var post = await client.PostAsJsonAsync("/api/v1/weather-forecasts", ValidPayload());

        post.StatusCode.Should().Be(HttpStatusCode.Created);
        factory.CacheService.Contains(DefaultGetCacheKey)
            .Should().BeFalse("POST should have evicted the cache entry");

        // Step 3 — GET again: cache miss → handler runs again → stored again.
        await client.GetAsync("/api/v1/weather-forecasts");

        factory.CacheService.SetCalls.Count.Should().BeGreaterThan(setCountAfterGet,
            "the second GET must re-populate the cache after invalidation");
    }

    [Fact]
    public async Task MultipleSuccessfulPosts_EachInvalidatesCache()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/weather-forecasts", ValidPayload());
        await client.PostAsJsonAsync("/api/v1/weather-forecasts", ValidPayload());

        factory.CacheService.RemoveByPrefixCalls
            .Should().HaveCount(2, "each successful POST must trigger its own invalidation");
        factory.CacheService.RemoveByPrefixCalls
            .Should().AllBe(ExpectedInvalidationPrefix);
    }

    [Fact]
    public async Task InvalidationDoesNotAffectUnrelatedCacheKeys()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        // Pre-populate an unrelated key that shares no prefix with weather-forecasts.
        factory.CacheService.Seed("api:v1:api/v1/users:_:_:_", new { id = 1, name = "Alice" });

        await client.PostAsJsonAsync("/api/v1/weather-forecasts", ValidPayload());

        // Spy's RemoveByPrefixAsync only removes keys under the weather-forecasts prefix.
        factory.CacheService.Contains("api:v1:api/v1/users:_:_:_")
            .Should().BeTrue("POST to weather-forecasts must not evict unrelated cache keys");
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static object ValidPayload() => new
    {
        date = "2024-01-15T00:00:00",
        temperatureC = 20,
        summary = "Sunny"
    };
}
