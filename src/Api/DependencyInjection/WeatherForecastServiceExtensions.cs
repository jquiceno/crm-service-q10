using WeatherForecast.Application.UseCases.CreateWeatherForecast;
using WeatherForecast.Application.UseCases.GetWeatherForecast;
using FluentValidation;

namespace Api.DependencyInjection;

public static class WeatherForecastServiceExtensions
{
    public static IServiceCollection AddWeatherForecastServices(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<CreateWeatherForecastInputValidator>();
        services.AddScoped<IGetWeatherForecastUseCase, GetWeatherForecastUseCase>();
        services.AddScoped<ICreateWeatherForecastUseCase, CreateWeatherForecastUseCase>();

        return services;
    }
}
