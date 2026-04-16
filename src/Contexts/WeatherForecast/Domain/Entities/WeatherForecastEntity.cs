using Shared.Domain;

namespace WeatherForecast.Domain.Entities;

public sealed class WeatherForecastEntity : Entity
{
    public DateTime Date { get; private set; }
    public int TemperatureC { get; private set; }
    public string Summary { get; private set; } = null!;
    public int TemperatureF => 32 + (int)(TemperatureC * 9.0 / 5.0);

    private WeatherForecastEntity() { }

    internal WeatherForecastEntity(Guid id, DateTime date, int temperatureC, string summary)
        : base(id)
    {
        Date = date;
        TemperatureC = temperatureC;
        Summary = summary;
    }
}
