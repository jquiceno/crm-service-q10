using Shared.Domain.Pagination;
using Shared.Domain.Result;
using WeatherForecast.Domain.Aggregates;

namespace WeatherForecast.Domain.Ports;

public interface IWeatherForecastRepositoryPort
{
    Task<Result<WeatherForecastAggregate>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<WeatherForecastAggregate>> GetAllAsync(PageQuery page, CancellationToken cancellationToken = default);
    Task<Result> AddAsync(WeatherForecastAggregate aggregate, CancellationToken cancellationToken = default);
    Result Update(WeatherForecastAggregate aggregate);
    Result Remove(WeatherForecastAggregate aggregate);
    Task<Result<bool>> ExistsForDateAsync(DateTime date, CancellationToken cancellationToken = default);
}
