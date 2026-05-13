using Shared.Application.Dtos;

namespace WeatherForecast.Application.UseCases.GetWeatherForecast;

public sealed record GetWeatherForecastOutputDto(
    Guid Id, DateTime Date, int Temperature, int TemperatureFahrenheit, string Summary,
    AddressOutputDto? Address);
