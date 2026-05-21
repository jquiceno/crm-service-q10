using Infrastructure.Persistence.EntityFramework;
using Infrastructure.Persistence.EntityFramework.Common;
using Microsoft.EntityFrameworkCore;
using Shared.Application.Ports;
using Shared.Domain.Result;
using WeatherForecast.Domain.Aggregates;
using WeatherForecast.Domain.Entities;
using WeatherForecast.Domain.Errors;
using WeatherForecast.Domain.Ports;
using Shared.Domain.Errors;

namespace Infrastructure.Adapters.Persistence.WeatherForecast;

public sealed class WeatherForecastRepositoryAdapter(
    ApplicationDbContext context,
    ILoggerPort<WeatherForecastRepositoryAdapter> logger)
    : BaseAggregateRepository<WeatherForecastAggregate, WeatherForecastEntity>(context, logger),
      IWeatherForecastRepositoryPort
{
    protected override WeatherForecastAggregate ToAggregate(WeatherForecastEntity entity)
        => WeatherForecastAggregate.FromEntity(entity);

    protected override WeatherForecastEntity ToEntity(WeatherForecastAggregate aggregate)
        => aggregate.ToEntity();

    protected override DomainError GetNotFoundError(Guid id) =>
        WeatherForecastErrors.NotFound(id);

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
            logger.Error(ex, "Error checking if forecast exists for date {Date}", date);
            return PersistenceErrors.Failure();
        }
    }
}
