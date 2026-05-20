using Shared.Domain.Result;
using WeatherForecast.Domain.Aggregates;

namespace WeatherForecast.Domain.Interfaces;

public interface IWeatherForecastRepository
{
    Task<Result<WeatherForecastAggregate>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<WeatherForecastAggregate>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result> AddAsync(WeatherForecastAggregate aggregate, CancellationToken cancellationToken = default);
    Result Update(WeatherForecastAggregate aggregate);
    Result Remove(WeatherForecastAggregate aggregate);
    Task<Result<bool>> ExistsForDateAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default);
}
