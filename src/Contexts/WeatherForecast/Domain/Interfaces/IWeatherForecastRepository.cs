using WeatherForecast.Domain.Aggregates;

namespace WeatherForecast.Domain.Interfaces;

public interface IWeatherForecastRepository
{
    Task<WeatherForecastAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WeatherForecastAggregate>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(WeatherForecastAggregate aggregate, CancellationToken cancellationToken = default);
    void Update(WeatherForecastAggregate aggregate);
    void Remove(WeatherForecastAggregate aggregate);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
