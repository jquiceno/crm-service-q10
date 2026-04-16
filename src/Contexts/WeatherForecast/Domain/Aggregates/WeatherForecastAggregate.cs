using Shared.Domain;
using WeatherForecast.Domain.Entities;

namespace WeatherForecast.Domain.Aggregates;

public sealed class WeatherForecastAggregate : AggregateRoot<WeatherForecastEntity>
{
    public const int MinTemperatureC = -60;
    public const int MaxTemperatureC = 60;
    public const int MaxSummaryLength = 200;

    public DateTime Date => _entity.Date;
    public int TemperatureC => _entity.TemperatureC;
    public string Summary => _entity.Summary;
    public int TemperatureF => _entity.TemperatureF;
    public DateTime CreatedAtUtc => _entity.CreatedAtUtc;

    private WeatherForecastAggregate(WeatherForecastEntity entity) : base(entity) { }

    public static WeatherForecastAggregate Create(Guid id, DateTime date, int temperatureC, string summary)
    {
        var entity = new WeatherForecastEntity(id, date, temperatureC, summary);
        return new WeatherForecastAggregate(entity);
    }

    public WeatherForecastEntity ToEntity() => _entity;

    public static WeatherForecastAggregate FromEntity(WeatherForecastEntity entity)
        => new(entity);
}
