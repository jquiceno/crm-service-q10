namespace WeatherForecast.Application.UseCases.CreateWeatherForecast;

public sealed record CreateWeatherForecastInputDto(DateTime Date, int TemperatureC, string? Summary);
