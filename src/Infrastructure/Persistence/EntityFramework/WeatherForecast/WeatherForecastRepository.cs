using Infrastructure.Persistence.EntityFramework.Common;
using Microsoft.EntityFrameworkCore;
using Shared.Domain.Errors;
using Shared.Domain.Result;
using WeatherForecast.Domain.Aggregates;
using WeatherForecast.Domain.Entities;
using WeatherForecast.Domain.Errors;
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

    protected override DomainError GetNotFoundError(Guid id) => WeatherForecastErrors.NotFound(id) with { Context = WeatherForecastErrors.Context };

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
            return SharedErrors.PersistenceFailure(ex.Message);
        }
    }
}
