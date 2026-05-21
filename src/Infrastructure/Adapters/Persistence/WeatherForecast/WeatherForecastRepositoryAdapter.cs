using Infrastructure.Persistence.EntityFramework;
using Infrastructure.Persistence.EntityFramework.Common;
using Microsoft.EntityFrameworkCore;
using Shared.Domain.Result;
using WeatherForecast.Domain.Aggregates;
using WeatherForecast.Domain.Entities;
using WeatherForecast.Domain.Ports;

namespace Infrastructure.Adapters.Persistence.WeatherForecast;

public sealed class WeatherForecastRepositoryAdapter(ApplicationDbContext context)
    : BaseAggregateRepository<WeatherForecastAggregate, WeatherForecastEntity, Guid>(context),
      IWeatherForecastRepositoryPort
{
    protected override WeatherForecastAggregate ToAggregate(WeatherForecastEntity entity)
        => WeatherForecastAggregate.FromEntity(entity);

    protected override WeatherForecastEntity ToEntity(WeatherForecastAggregate aggregate)
        => aggregate.ToEntity();

    public async Task<Result<bool>> ExistsForDateAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        try
        {
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1);
            return await DbSet.AnyAsync(e => e.Date >= startOfDay && e.Date < endOfDay, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return PersistenceErrors.Failure();
        }
    }
}
