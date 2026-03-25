namespace WeatherForecast.Application.UseCases.GetWeatherForecast;

public sealed record GetWeatherForecastOutputDto(
    Guid Id, DateTime Date, int TemperatureC, int TemperatureF, string Summary);
