using Shared.Domain.Interfaces;
using Shared.Domain.Result;
using WeatherForecast.Domain.Aggregates;

namespace WeatherForecast.Domain.Ports;

public interface IWeatherForecastRepositoryPort : IRepositoryBase<WeatherForecastAggregate, Guid>
{
    Task<Result<bool>> ExistsForDateAsync(DateTime date, CancellationToken cancellationToken = default);
}
