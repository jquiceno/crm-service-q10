using Shared.Domain;

namespace WeatherForecast.Domain.Entities;

public sealed class WeatherForecastEntity : Entity, IAggregateRoot
{
    public const int MinTemperatureC = -60;
    public const int MaxTemperatureC = 60;
    public const int MaxSummaryLength = 200;

    public DateTime Date { get; private set; }
    public int TemperatureC { get; private set; }
    public string Summary { get; private set; } = null!;
    public int TemperatureF => 32 + (int)(TemperatureC * 9.0 / 5.0);

    private WeatherForecastEntity() { }

    private WeatherForecastEntity(Guid id, DateTime date, int temperatureC, string summary)
        : base(id)
    {
        Date = date;
        TemperatureC = temperatureC;
        Summary = summary;
    }

    public static WeatherForecastEntity Create(
        Guid id,
        DateTime date,
        int temperatureC,
        string summary)
    {
        return new WeatherForecastEntity(id, date, temperatureC, summary);
    }
}
