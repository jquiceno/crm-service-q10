using WeatherForecast.Domain.Entities;

namespace WeatherForecast.Domain.Interfaces;

public interface IWeatherForecastRepository
{
    Task<WeatherForecastEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WeatherForecastEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(WeatherForecastEntity entity, CancellationToken cancellationToken = default);
    void Update(WeatherForecastEntity entity);
    void Remove(WeatherForecastEntity entity);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
