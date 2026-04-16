using Infrastructure.Persistence.EntityFramework.Common;
using WeatherForecast.Domain.Aggregates;
using WeatherForecast.Domain.Entities;
using WeatherForecast.Domain.Interfaces;

namespace Infrastructure.Persistence.EntityFramework.WeatherForecast;

public sealed class WeatherForecastRepository(ApplicationDbContext context)
    : BaseAggregateRepository<WeatherForecastAggregate, WeatherForecastEntity>(context),
      IWeatherForecastRepository
{
    protected override WeatherForecastAggregate ToAggregate(WeatherForecastEntity entity)
        => WeatherForecastAggregate.FromEntity(entity);

    protected override WeatherForecastEntity ToEntity(WeatherForecastAggregate aggregate)
        => aggregate.ToEntity();
}
