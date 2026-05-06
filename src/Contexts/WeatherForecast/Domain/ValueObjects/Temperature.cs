using Shared.Domain;
using WeatherForecast.Domain.Errors;

namespace WeatherForecast.Domain.ValueObjects;

public sealed class Temperature : ValueObject
{
    public const int MinCelsius = -60;
    public const int MaxCelsius = 60;

    public int Celsius { get; }
    public int Fahrenheit => (int)Math.Round(Celsius * 9.0 / 5.0 + 32);

    private Temperature(int celsius) { Celsius = celsius; }

    public static Result<Temperature, ValidationError> Create(int celsius)
    {
        if (celsius < MinCelsius || celsius > MaxCelsius)
            return WeatherForecastErrors.TemperatureOutOfRange;

        return new Temperature(celsius);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Celsius;
    }
}
