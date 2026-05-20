using WeatherForecast.Domain.Aggregates;

namespace WeatherForecast.Domain.Ports;

public interface IWeatherForecastRepositoryPort
{
    Task<WeatherForecastAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WeatherForecastAggregate>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(WeatherForecastAggregate aggregate, CancellationToken cancellationToken = default);
    void Update(WeatherForecastAggregate aggregate);
    void Remove(WeatherForecastAggregate aggregate);
    Task<bool> ExistsForDateAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
