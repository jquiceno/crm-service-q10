using Infrastructure.Persistence.EntityFramework.WeatherForecast;
using WeatherForecast.Application.UseCases.CreateWeatherForecast;
using WeatherForecast.Application.UseCases.GetWeatherForecast;
using WeatherForecast.Domain.Interfaces;

namespace Api.DependencyInjection;

public static class WeatherForecastServiceExtensions
{
    public static IServiceCollection AddWeatherForecastServices(this IServiceCollection services)
    {
        services.AddScoped<IGetWeatherForecastUseCase, GetWeatherForecastUseCase>();
        services.AddScoped<ICreateWeatherForecastUseCase, CreateWeatherForecastUseCase>();
        services.AddScoped<IWeatherForecastRepository, WeatherForecastRepository>();

        return services;
    }
}
