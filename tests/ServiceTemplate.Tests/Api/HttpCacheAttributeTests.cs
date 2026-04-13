using System.Net.Http.Json;
using FluentAssertions;
using Infrastructure.Cache;
using Xunit;

namespace ServiceTemplate.Tests.Api;

/// <summary>
/// Verifies the <c>[HttpCache]</c> action filter behaviour end-to-end using the
/// real ASP.NET Core pipeline and a <see cref="TestWebApplicationFactory"/> that
/// swaps Redis for an in-memory spy.
///
/// Each test creates its own factory so spy state is completely isolated.
/// </summary>
public sealed class HttpCacheAttributeTests
{
    // The expected cache key for an anonymous GET /api/v1/weather-forecasts
    // with no query string, tenant, or locale headers.
    private static readonly string DefaultCacheKey =
        CacheKeyBuilder.Build("api:v1", "api/v1/weather-forecasts", null, null, null);

    [Fact]
    public async Task GetAll_CacheMiss_ExecutesHandlerAndStoresResponseInCache()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/weather-forecasts");

        response.IsSuccessStatusCode.Should().BeTrue();

        // Filter must have checked the cache once …
        factory.CacheService.GetCalls.Should().Contain(DefaultCacheKey);

        // … and stored the result after executing the handler.
        factory.CacheService.SetCalls
            .Should().ContainSingle(c => c.Key == DefaultCacheKey,
                "the handler result must be written to cache on a miss");
    }

    [Fact]
    public async Task GetAll_CacheMiss_AppliesPerEndpointTtl()
    {
        // WeatherForecastController declares [HttpCache(ttlSeconds: 60)]
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        await client.GetAsync("/api/v1/weather-forecasts");

        factory.CacheService.SetCalls
            .Should().ContainSingle(c => c.Ttl == TimeSpan.FromSeconds(60),
                "per-endpoint TTL from [HttpCache(ttlSeconds: 60)] must be respected");
    }

    [Fact]
    public async Task GetAll_CacheHit_ReturnsCachedResponseWithoutExecutingHandler()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        // Pre-seed the cache with a distinctive payload.
        var seeded = new[] { new { id = Guid.NewGuid(), summary = "Seeded forecast", temperatureC = 99 } };
        factory.CacheService.Seed(DefaultCacheKey, seeded);

        var response = await client.GetAsync("/api/v1/weather-forecasts");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue();

        // Response body must come from the cache, not from the in-memory DB.
        body.Should().Contain("Seeded forecast");

        // SetAsync must NOT have been called — the handler was short-circuited.
        factory.CacheService.SetCalls.Should().BeEmpty(
            "a cache hit must skip the action and therefore skip the cache write");
    }

    [Fact]
    public async Task GetAll_CacheHit_DoesNotCallHandlerMoreThanOnce()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        // First request: miss → populates cache.
        await client.GetAsync("/api/v1/weather-forecasts");
        var setCountAfterFirst = factory.CacheService.SetCalls.Count;

        // Second request: hit → should not write to cache again.
        await client.GetAsync("/api/v1/weather-forecasts");

        factory.CacheService.SetCalls.Count.Should().Be(setCountAfterFirst,
            "a cache hit on the second request must not trigger a second write");
    }

    [Fact]
    public async Task GetAll_DifferentTenantHeaders_ProduceDifferentCacheKeys()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        await SendGet(client, tenant: "tenant-a");
        await SendGet(client, tenant: "tenant-b");

        var keys = factory.CacheService.SetCalls.Select(c => c.Key).ToList();

        keys.Should().HaveCount(2);
        keys.Should().OnlyHaveUniqueItems("each tenant must get a separate cache entry");
    }

    [Fact]
    public async Task GetAll_DifferentLocales_ProduceDifferentCacheKeys()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        await SendGet(client, locale: "en-US");
        await SendGet(client, locale: "es-CO");

        var keys = factory.CacheService.SetCalls.Select(c => c.Key).ToList();

        keys.Should().HaveCount(2);
        keys.Should().OnlyHaveUniqueItems("each locale must get a separate cache entry");
    }

    [Fact]
    public async Task GetAll_SameTenantSameLocale_UsesSameCacheKey()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        await SendGet(client, tenant: "corp", locale: "fr-FR");
        await SendGet(client, tenant: "corp", locale: "fr-FR");

        // First request is a miss → writes. Second is a hit → no write.
        factory.CacheService.SetCalls.Should().ContainSingle(
            "identical tenant + locale must hit the same cache slot on the second request");
    }

    [Fact]
    public async Task Post_IsNotInterceptedByCache()
    {
        // POST requests must bypass the HttpCacheAttribute entirely.
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/weather-forecasts", new
        {
            date = "2024-01-15T00:00:00",
            temperatureC = 20,
            summary = "Sunny"
        });

        factory.CacheService.GetCalls.Should().BeEmpty("POST must not read from cache");
        factory.CacheService.SetCalls.Should().BeEmpty("POST must not write to cache");
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static Task<HttpResponseMessage> SendGet(
        HttpClient client,
        string? tenant = null,
        string? locale = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/weather-forecasts");

        if (tenant is not null)
            request.Headers.Add("X-Tenant-Id", tenant);

        if (locale is not null)
            request.Headers.Add("Accept-Language", locale);

        return client.SendAsync(request);
    }
}
