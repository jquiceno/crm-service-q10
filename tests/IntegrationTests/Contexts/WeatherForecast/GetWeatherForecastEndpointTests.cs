using System.Net;
using System.Net.Http.Json;
using IntegrationTests.Infrastructure;
using Shouldly;
using WeatherForecast.Application.UseCases.GetWeatherForecast;
using WeatherForecast.Domain.Entities;
using Xunit;

namespace IntegrationTests.Contexts.WeatherForecast;

[Collection(IntegrationTestCollection.Name)]
public sealed class GetWeatherForecastEndpointTests : IntegrationTestBase
{
    public GetWeatherForecastEndpointTests(SqlServerContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetAll_WithSeededRows_Returns200_AndPayload()
    {
        var seeded = new WeatherForecastEntity(Guid.NewGuid(), new DateTime(2026, 4, 21), 25, "Sunny");
        Db.Set<WeatherForecastEntity>().Add(seeded);
        await Db.SaveChangesAsync();

        var response = await Client.GetAsync("/api/v1/weather-forecasts");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<GetWeatherForecastOutputDto>>();
        body.ShouldNotBeNull();
        body!.Count.ShouldBe(1);
        body[0].Id.ShouldBe(seeded.Id);
        body[0].Summary.ShouldBe("Sunny");
        body[0].TemperatureC.ShouldBe(25);
    }

    [Fact]
    public async Task GetAll_WithEmptyDatabase_Returns200_AndEmptyArray()
    {
        var response = await Client.GetAsync("/api/v1/weather-forecasts");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<GetWeatherForecastOutputDto>>();
        body.ShouldNotBeNull();
        body!.ShouldBeEmpty();
    }
}
