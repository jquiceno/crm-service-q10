using Infrastructure.Persistence.Common;
using WeatherForecast.Domain.Entities;
using WeatherForecast.Domain.Interfaces;

namespace Infrastructure.Persistence.WeatherForecast;

public class WeatherForecastRepository(ApplicationDbContext context)
    : BaseRepository<WeatherForecastEntity>(context), IWeatherForecastRepository
{
}
