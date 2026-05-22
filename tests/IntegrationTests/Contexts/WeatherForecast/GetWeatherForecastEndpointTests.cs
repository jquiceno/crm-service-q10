using System.Net;
using System.Net.Http.Json;
using Api.Responses;
using IntegrationTests.Infrastructure;
using Shouldly;
using WeatherForecast.Application.UseCases.GetWeatherForecast;
using WeatherForecast.Domain.Aggregates;
using WeatherForecast.Domain.Entities;
using Xunit;

namespace IntegrationTests.Contexts.WeatherForecast;

[Collection(IntegrationTestCollection.Name)]
public sealed class GetWeatherForecastEndpointTests : IntegrationTestBase
{
    private const string Route = "/api/v1/weather-forecast";

    public GetWeatherForecastEndpointTests(SqlServerContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetAll_WithSeededRows_Returns200_AndPayload()
    {
        var aggregate = WeatherForecastAggregate.Create(Guid.NewGuid(), new DateTime(2026, 4, 21), 25, "Sunny", null);
        var seeded = aggregate.Value.ToEntity();
        Db.Set<WeatherForecastEntity>().Add(seeded);
        await Db.SaveChangesAsync();

        var response = await Client.GetAsync(Route);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<ApiSuccessResponse<ApiPagedData<GetWeatherForecastOutputDto>>>();
        envelope.ShouldNotBeNull();
        envelope!.StatusCode.ShouldBe(200);
        envelope.Data.Items.Count.ShouldBe(1);
        envelope.Data.TotalCount.ShouldBe(1);
        envelope.Data.Items[0].Id.ShouldBe(seeded.Id);
        envelope.Data.Items[0].Summary.ShouldBe("Sunny");
        envelope.Data.Items[0].Temperature.ShouldBe(25);
    }

    [Fact]
    public async Task GetAll_WithEmptyDatabase_Returns200_AndEmptyArray()
    {
        var response = await Client.GetAsync(Route);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<ApiSuccessResponse<ApiPagedData<GetWeatherForecastOutputDto>>>();
        envelope.ShouldNotBeNull();
        envelope!.StatusCode.ShouldBe(200);
        envelope.Data.Items.ShouldBeEmpty();
        envelope.Data.TotalCount.ShouldBe(0);
    }
}
