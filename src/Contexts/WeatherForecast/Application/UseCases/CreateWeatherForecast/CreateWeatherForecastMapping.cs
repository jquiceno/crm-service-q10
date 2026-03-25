using WeatherForecast.Domain.Entities;

namespace WeatherForecast.Application.UseCases.CreateWeatherForecast;

public static class CreateWeatherForecastMapping
{
    public static WeatherForecastEntity ToEntity(this CreateWeatherForecastInputDto input) =>
        new(Guid.NewGuid(), input.Date, input.TemperatureC, input.Summary);

    public static CreateWeatherForecastOutputDto ToCreateDto(this WeatherForecastEntity entity) =>
        new(entity.Id, entity.Date, entity.TemperatureC, entity.TemperatureF, entity.Summary, entity.CreatedAtUtc);
}
