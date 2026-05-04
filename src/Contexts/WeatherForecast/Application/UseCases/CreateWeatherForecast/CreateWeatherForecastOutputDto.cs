namespace WeatherForecast.Application.UseCases.CreateWeatherForecast;

public sealed record CreateWeatherForecastOutputDto(
    Guid Id, DateTime Date, int Temperature, int TemperatureFahrenheit, string Summary, DateTime CreatedAtUtc);
