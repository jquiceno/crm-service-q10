using WeatherForecast.Domain.Common;

namespace WeatherForecast.Domain.Entities;

public sealed class WeatherForecastEntity : Entity, IAggregateRoot
{
    public DateTime Date { get; private set; }
    public int TemperatureC { get; private set; }
    public string Summary { get; private set; } = null!;
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

    private WeatherForecastEntity() { }

    public WeatherForecastEntity(Guid id, DateTime date, int temperatureC, string summary)
        : base(id)
    {
        Date = date;
        TemperatureC = temperatureC;
        Summary = summary ?? throw new ArgumentNullException(nameof(summary));
    }
}
