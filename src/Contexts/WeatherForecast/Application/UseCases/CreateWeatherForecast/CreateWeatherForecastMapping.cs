using WeatherForecast.Domain.Aggregates;

namespace WeatherForecast.Application.UseCases.CreateWeatherForecast;

public static class CreateWeatherForecastMapping
{
    public static WeatherForecastAggregate ToAggregate(this CreateWeatherForecastInputDto input) =>
        WeatherForecastAggregate.Create(Guid.NewGuid(), input.Date, input.TemperatureC, input.Summary);

    public static CreateWeatherForecastOutputDto ToCreateDto(this WeatherForecastAggregate aggregate) =>
        new(aggregate.Id, aggregate.Date, aggregate.TemperatureC, aggregate.TemperatureF, aggregate.Summary, aggregate.CreatedAtUtc);
}
