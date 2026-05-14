using System.Net;
using System.Net.Http.Json;
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
    public CreateWeatherForecastEndpointTests(SqlServerContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Create_WithValidPayload_Returns201_AndPersistsRow()
    {
        var input = new CreateWeatherForecastInputDto(new DateTime(2026, 4, 21), 25, "Sunny");

        var response = await Client.PostAsJsonAsync("/api/v1/weather-forecasts", input);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<CreateWeatherForecastOutputDto>();
        body.ShouldNotBeNull();
        body!.Summary.ShouldBe("Sunny");
        body.TemperatureC.ShouldBe(25);

        var persisted = await Db.Set<WeatherForecastEntity>()
            .AsNoTracking()
            .SingleAsync(e => e.Id == body.Id);

        persisted.Summary.ShouldBe("Sunny");
        persisted.TemperatureC.ShouldBe(25);
    }

    [Fact]
    public async Task Create_WithInvalidPayload_Returns400_WithValidationError()
    {
        var input = new CreateWeatherForecastInputDto(new DateTime(2026, 4, 21), 999, "");

        var response = await Client.PostAsJsonAsync("/api/v1/weather-forecasts", input);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("TemperatureC");
        body.ShouldContain("Summary");
    }
}
