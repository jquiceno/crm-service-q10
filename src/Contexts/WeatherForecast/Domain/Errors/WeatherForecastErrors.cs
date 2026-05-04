using Shared.Domain;
using WeatherForecast.Domain.Aggregates;
using WeatherForecast.Domain.ValueObjects;

namespace WeatherForecast.Domain.Errors;

public static class WeatherForecastErrors
{
    public const string Context = "WeatherForecast";

    public static readonly ValidationError DateRequired =
        new("Date is required.", ErrorType.Validation);

    public static readonly ValidationError TemperatureOutOfRange =
        new($"Temperature must be between {Temperature.MinCelsius} and {Temperature.MaxCelsius}.",
            ErrorType.Validation)
        {
            Details = new Dictionary<string, object?>
            {
                ["min"] = Temperature.MinCelsius,
                ["max"] = Temperature.MaxCelsius
            }
        };

    public static readonly ValidationError SummaryRequired =
        new("Summary is required.", ErrorType.Validation);

    public static readonly ValidationError SummaryTooLong =
        new($"Summary must not exceed {WeatherForecastAggregate.MaxSummaryLength} characters.",
            ErrorType.Validation);

    public static readonly ValidationError DateAlreadyExists =
        new("A forecast for this date already exists.", ErrorType.Conflict);
}
