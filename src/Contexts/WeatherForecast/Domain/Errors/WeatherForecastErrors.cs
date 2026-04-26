using Shared.Domain;
using WeatherForecast.Domain.Aggregates;

namespace WeatherForecast.Domain.Errors;

public static class WeatherForecastErrors
{
    public static readonly Error DateRequired =
        new("WEATHER_FORECAST.DATE_REQUIRED", "Date is required.", ErrorType.Validation);

    public static readonly Error SummaryRequired =
        new("WEATHER_FORECAST.SUMMARY_REQUIRED", "Summary is required.", ErrorType.Validation);

    public static readonly Error SummaryTooLong =
        new("WEATHER_FORECAST.SUMMARY_TOO_LONG",
            $"Summary must not exceed {WeatherForecastAggregate.MaxSummaryLength} characters.",
            ErrorType.Validation)
        {
            Context = new Dictionary<string, object?>
            {
                ["maxLength"] = WeatherForecastAggregate.MaxSummaryLength
            }
        };

    public static readonly Error DateAlreadyExists =
        new("WEATHER_FORECAST.DATE_ALREADY_EXISTS",
            "A forecast for this date already exists.",
            ErrorType.Conflict);
}
