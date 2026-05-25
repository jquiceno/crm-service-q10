namespace Api.DependencyInjection;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSharedServices();
        services.AddWeatherForecastServices();
        services.AddHealthServices();

        return services;
    }
}
