using WeatherForecast.Domain.Errors;
using WeatherForecast.Domain.Common;

namespace WeatherForecast.Domain.Entities;

public sealed class WeatherForecastEntity : Entity, IAggregateRoot
{
    public const int MinTemperatureC = -60;
    public const int MaxTemperatureC = 60;

    public DateTime Date { get; private set; }
    public int TemperatureC { get; private set; }
    public string Summary { get; private set; } = null!;
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

    private WeatherForecastEntity() { }

    private WeatherForecastEntity(Guid id, DateTime date, int temperatureC, string summary)
        : base(id)
    {
        Date = date;
        TemperatureC = temperatureC;
        Summary = summary ?? throw new ArgumentNullException(nameof(summary));
    }

    public static Result<WeatherForecastEntity> Create(
        Guid id,
        DateTime date,
        int temperatureC,
        string summary)
    {
        if (temperatureC < MinTemperatureC || temperatureC > MaxTemperatureC)
            return Result<WeatherForecastEntity>.Failure(WeatherForecastErrors.TemperatureOutOfRange);

        return Result<WeatherForecastEntity>.Success(new WeatherForecastEntity(id, date, temperatureC, summary));
    }
}
