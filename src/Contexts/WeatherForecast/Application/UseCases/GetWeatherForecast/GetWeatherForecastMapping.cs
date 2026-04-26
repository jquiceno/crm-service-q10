using WeatherForecast.Domain.Aggregates;

namespace WeatherForecast.Application.UseCases.GetWeatherForecast;

public static class GetWeatherForecastMapping
{
    public static GetWeatherForecastOutputDto ToGetDto(this WeatherForecastAggregate aggregate) =>
        new(aggregate.Id,
            aggregate.Date,
            aggregate.Temperature.Celsius,
            aggregate.Temperature.Fahrenheit,
            aggregate.Summary);
}
