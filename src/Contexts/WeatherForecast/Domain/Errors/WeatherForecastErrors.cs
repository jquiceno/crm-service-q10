using WeatherForecast.Domain.Common;

namespace WeatherForecast.Domain.Errors;

public static class WeatherForecastErrors
{
    public static readonly Error TemperatureOutOfRange =
        new("WeatherForecast.TemperatureOutOfRange", "TemperatureC must be between -60 and 60.");
}
