using Shared.Domain;
using WeatherForecast.Domain.ValueObjects;

namespace WeatherForecast.Domain.Entities;

public sealed class WeatherForecastEntity : Entity
{
    public DateTime Date { get; private set; }
    public Temperature Temperature { get; private set; } = null!;
    public string Summary { get; private set; } = null!;

    private WeatherForecastEntity() { }

    internal WeatherForecastEntity(Guid id, DateTime date, Temperature temperature, string summary)
        : base(id)
    {
        Date = date;
        Temperature = temperature;
        Summary = summary;
    }
}
