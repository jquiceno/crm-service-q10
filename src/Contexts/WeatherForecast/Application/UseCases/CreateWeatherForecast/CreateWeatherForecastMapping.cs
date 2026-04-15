using WeatherForecast.Domain.Entities;
using WeatherForecast.Domain.Common;

namespace WeatherForecast.Application.UseCases.CreateWeatherForecast;

public static class CreateWeatherForecastMapping
{
    public static Result<WeatherForecastEntity> ToEntity(this CreateWeatherForecastInputDto input) =>
        WeatherForecastEntity.Create(Guid.NewGuid(), input.Date, input.TemperatureC, input.Summary);

    public static CreateWeatherForecastOutputDto ToCreateDto(this WeatherForecastEntity entity) =>
        new(entity.Id, entity.Date, entity.TemperatureC, entity.TemperatureF, entity.Summary, entity.CreatedAtUtc);
}
