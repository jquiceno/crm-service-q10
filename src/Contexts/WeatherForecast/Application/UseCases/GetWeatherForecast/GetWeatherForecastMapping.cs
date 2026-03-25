using WeatherForecast.Domain.Entities;

namespace WeatherForecast.Application.UseCases.GetWeatherForecast;

public static class GetWeatherForecastMapping
{
    public static GetWeatherForecastOutputDto ToGetDto(this WeatherForecastEntity entity) =>
        new(entity.Id, entity.Date, entity.TemperatureC, entity.TemperatureF, entity.Summary);
}
