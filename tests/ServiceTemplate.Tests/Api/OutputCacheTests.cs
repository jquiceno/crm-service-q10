using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace ServiceTemplate.Tests.Api;

/// <summary>
/// End-to-end behaviour of ASP.NET Core OutputCaching as wired in
/// <c>Program.cs</c>, plus the custom <c>[OutputCacheInvalidate]</c> filter.
/// Assertions rely on a counting decorator around the Get use case — a second
/// request with no handler execution means the response came from cache.
/// </summary>
public sealed class OutputCacheTests
{
    [Fact]
    public async Task GetAll_SecondRequest_IsServedFromCacheWithoutExecutingUseCase()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        (await client.GetAsync("/api/v1/weather-forecasts")).IsSuccessStatusCode.Should().BeTrue();
        (await client.GetAsync("/api/v1/weather-forecasts")).IsSuccessStatusCode.Should().BeTrue();

        factory.GetUseCase.Executions
            .Should().Be(1, "the second GET must be served from output cache");
    }

    [Fact]
    public async Task GetAll_DifferentTenantHeaders_BypassCache()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        await SendGet(client, tenant: "tenant-a");
        await SendGet(client, tenant: "tenant-b");

        factory.GetUseCase.Executions
            .Should().Be(2, "each tenant must get its own cache entry");
    }

    [Fact]
    public async Task GetAll_DifferentLocales_BypassCache()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        await SendGet(client, locale: "en-US");
        await SendGet(client, locale: "es-CO");

        factory.GetUseCase.Executions
            .Should().Be(2, "each locale must get its own cache entry");
    }

    [Fact]
    public async Task GetAll_SameTenantAndLocale_HitsCache()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        await SendGet(client, tenant: "corp", locale: "fr-FR");
        await SendGet(client, tenant: "corp", locale: "fr-FR");

        factory.GetUseCase.Executions.Should().Be(1);
    }

    [Fact]
    public async Task Post_SuccessfulMutation_InvalidatesCachedGet_EndToEnd()
    {
        // Integration proof: [OutputCache] + [OutputCacheInvalidate] wire together.
        // Edge cases (failure, exception, multi-tag) are covered as unit tests in
        // OutputCacheInvalidateAttributeTests.
        await using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        await client.GetAsync("/api/v1/weather-forecasts");
        factory.GetUseCase.Executions.Should().Be(1);

        var post = await client.PostAsJsonAsync("/api/v1/weather-forecasts", ValidPayload());
        post.StatusCode.Should().Be(HttpStatusCode.Created);

        await client.GetAsync("/api/v1/weather-forecasts");
        factory.GetUseCase.Executions.Should().Be(2, "invalidation must force a fresh handler run");
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static Task<HttpResponseMessage> SendGet(HttpClient client, string? tenant = null, string? locale = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/weather-forecasts");
        if (tenant is not null) request.Headers.Add("X-Tenant-Id", tenant);
        if (locale is not null) request.Headers.Add("Accept-Language", locale);
        return client.SendAsync(request);
    }

    private static object ValidPayload() => new
    {
        date = "2024-01-15T00:00:00",
        temperatureC = 20,
        summary = "Sunny"
    };
}
