using Infrastructure.Adapters.Persistence.WeatherForecast;
using WeatherForecast.Application.Ports;
using WeatherForecast.Application.UseCases.CreateWeatherForecast;
using WeatherForecast.Application.UseCases.GetWeatherForecast;
using WeatherForecast.Domain.Ports;

namespace Api.DependencyInjection;

public static class WeatherForecastServiceExtensions
{
    public static IServiceCollection AddWeatherForecastServices(this IServiceCollection services)
    {
        services.AddScoped<IGetWeatherForecastPort, GetWeatherForecastUseCase>();
        services.AddScoped<ICreateWeatherForecastPort, CreateWeatherForecastUseCase>();
        services.AddScoped<IWeatherForecastRepositoryPort, WeatherForecastRepositoryAdapter>();

        return services;
    }
}
