using System.ComponentModel;

namespace WeatherForecast.Application.UseCases.CreateWeatherForecast;

public sealed record CreateWeatherForecastInputDto(
    [property: Description("The date of the forecast.")]
    DateTime Date,
    [property: Description("Temperature in Celsius. Must be between -90 and 60.")]
    int TemperatureC,
    [property: Description("Short description, e.g. 'Sunny'. Max 200 characters.")]
    string Summary);
