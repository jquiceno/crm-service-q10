namespace WeatherForecast.Application.UseCases.CreateWeatherForecast;

public sealed record CreateWeatherForecastOutputDto(
    Guid Id, DateTime Date, int TemperatureC, int TemperatureF, string Summary, DateTime CreatedAtUtc);
