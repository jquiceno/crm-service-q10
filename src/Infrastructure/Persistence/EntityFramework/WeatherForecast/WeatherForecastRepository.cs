using Infrastructure.Persistence.EntityFramework.Common;
using Microsoft.EntityFrameworkCore;
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

    public async Task<bool> ExistsForDateAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);
        return await DbSet.AnyAsync(e => e.Date >= startOfDay && e.Date < endOfDay, cancellationToken);
    }
}