using ServiceInfo.Application.Ports;
using ServiceInfo.Application.UseCases.GetServiceInfo;
using Infrastructure.Adapters.ServiceInfo;

namespace Api.DependencyInjection;

public static class ServiceInfoServiceExtensions
{
    public static IServiceCollection AddServiceInfoServices(this IServiceCollection services)
    {
        services.AddSingleton<IGetServiceInfoPort, GetServiceInfoUseCase>();
        services.AddSingleton<IAppInfoPort, AppInfoAdapter>();

        return services;
    }
}
