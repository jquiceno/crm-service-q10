using Infrastructure.Persistence.EntityFramework.Common;
using WeatherForecast.Domain.Entities;
using WeatherForecast.Domain.Interfaces;

namespace Infrastructure.Persistence.EntityFramework.WeatherForecast;

public class WeatherForecastRepository(ApplicationDbContext context)
    : BaseRepository<WeatherForecastEntity>(context), IWeatherForecastRepository
{
}
