using Shared.Application.Dtos;
using WeatherForecast.Domain.Aggregates;

namespace WeatherForecast.Application.UseCases.GetWeatherForecast;

public static class GetWeatherForecastMapping
{
    public static GetWeatherForecastOutputDto ToGetDto(this WeatherForecastAggregate aggregate) =>
        new(aggregate.Id,
            aggregate.Date,
            aggregate.TemperatureCelsius,
            aggregate.TemperatureFahrenheit,
            aggregate.Summary,
            aggregate.AddressStreet is not null
                ? new AddressOutputDto(aggregate.AddressStreet, aggregate.AddressCity!, aggregate.AddressZipCode!)
                : null);
}
