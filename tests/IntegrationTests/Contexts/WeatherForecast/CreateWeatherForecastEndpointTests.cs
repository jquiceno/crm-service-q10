using System.Net;
using System.Net.Http.Json;
using Api.Responses;
using IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using WeatherForecast.Application.UseCases.CreateWeatherForecast;
using WeatherForecast.Domain.Entities;
using Xunit;

namespace IntegrationTests.Contexts.WeatherForecast;

[Collection(IntegrationTestCollection.Name)]
public sealed class CreateWeatherForecastEndpointTests : IntegrationTestBase
{
    private const string Route = "/api/v1/weather-forecast";

    public CreateWeatherForecastEndpointTests(SqlServerContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Create_WithValidPayload_Returns201_AndPersistsRow()
    {
        var input = new CreateWeatherForecastInputDto(new DateTime(2026, 4, 21), 25, "Sunny", null);

        var response = await Client.PostAsJsonAsync(Route, input);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var envelope = await response.Content.ReadFromJsonAsync<ApiSuccessResponse<CreateWeatherForecastOutputDto>>();
        envelope.ShouldNotBeNull();
        envelope!.StatusCode.ShouldBe(201);
        var body = envelope.Data;
        body.ShouldNotBeNull();
        body!.Summary.ShouldBe("Sunny");
        body.Temperature.ShouldBe(25);

        var persisted = await Db.Set<WeatherForecastEntity>()
            .AsNoTracking()
            .SingleAsync(e => e.Id == body.Id);

        persisted.Summary.ShouldBe("Sunny");
        persisted.Temperature.Celsius.ShouldBe(25);
    }

    [Fact]
    public async Task Create_WithInvalidPayload_Returns400_WithValidationError()
    {
        var input = new CreateWeatherForecastInputDto(new DateTime(2026, 4, 21), 999, "", null);

        var response = await Client.PostAsJsonAsync(Route, input);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("temperature");
        body.ShouldContain("summary");
    }
}
