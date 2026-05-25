using Health.Application.Ports;
using Health.Application.UseCases.GetHealthInfo;
using Infrastructure.Adapters.Health;

namespace Api.DependencyInjection;

public static class HealthServiceExtensions
{
    public static IServiceCollection AddHealthServices(this IServiceCollection services)
    {
        services.AddScoped<IGetHealthInfoPort, GetHealthInfoUseCase>();
        services.AddSingleton<IAppInfoPort, AppInfoAdapter>();

        return services;
    }
}
