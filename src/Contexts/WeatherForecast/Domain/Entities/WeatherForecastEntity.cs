using Shared.Domain.Entities;
using Shared.Domain.ValueObjects;
using WeatherForecast.Domain.ValueObjects;

namespace WeatherForecast.Domain.Entities;

public sealed class WeatherForecastEntity : Entity<Guid>
{
    public DateTime Date { get; private set; }
    public Temperature Temperature { get; private set; } = null!;
    public string Summary { get; private set; } = null!;
    public Address? Address { get; private set; }

    private WeatherForecastEntity() { }

    internal WeatherForecastEntity(Guid id, DateTime date, Temperature temperature, string summary, Address? address = null)
        : base(id)
    {
        Date = date;
        Temperature = temperature;
        Summary = summary;
        Address = address;
    }
}
