using Shared.Domain;
using WeatherForecast.Domain.Entities;
using WeatherForecast.Domain.ValueObjects;

namespace WeatherForecast.Domain.Aggregates;

public sealed class WeatherForecastAggregate : AggregateRoot<WeatherForecastEntity>
{
    public const int MaxSummaryLength = 200;

    public DateTime Date => _entity.Date;
    public Temperature Temperature => _entity.Temperature;
    public string Summary => _entity.Summary;
    public DateTime CreatedAtUtc => _entity.CreatedAtUtc;

    private WeatherForecastAggregate(WeatherForecastEntity entity) : base(entity) { }

    public static Result<WeatherForecastAggregate> Create(
        Guid id, DateTime date, int temperatureC, string summary)
    {
        var tempResult = Temperature.Create(temperatureC);
        if (tempResult.IsFailure)
            return tempResult.Error;

        var entity = new WeatherForecastEntity(id, date, tempResult.Value, summary);
        return new WeatherForecastAggregate(entity);
    }

    public WeatherForecastEntity ToEntity() => _entity;

    public static WeatherForecastAggregate FromEntity(WeatherForecastEntity entity)
        => new(entity);
}
