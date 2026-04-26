using Shared.Domain;

namespace WeatherForecast.Domain.ValueObjects;

public sealed class Temperature : ValueObject
{
    public const int MinCelsius = -60;
    public const int MaxCelsius = 60;

    public static readonly Error OutOfRangeError =
        new("WEATHER_FORECAST.TEMPERATURE_OUT_OF_RANGE",
            $"Temperature must be between {MinCelsius} and {MaxCelsius} °C.",
            ErrorType.Validation)
        {
            Context = new Dictionary<string, object?>
            {
                ["min"] = MinCelsius,
                ["max"] = MaxCelsius
            }
        };

    public int Celsius { get; }
    public int Fahrenheit => (int)Math.Round(Celsius * 9.0 / 5.0 + 32);

    private Temperature(int celsius) { Celsius = celsius; }

    public static Result<Temperature> Create(int celsius)
    {
        if (celsius < MinCelsius || celsius > MaxCelsius)
            return OutOfRangeError;

        return new Temperature(celsius);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Celsius;
    }
}
