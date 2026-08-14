using Api.Filters;
using Infrastructure.Adapters.Logging;
using Shared.Application.Ports;

namespace Api.DependencyInjection;

public static class SharedServiceExtensions
{
    public static IServiceCollection AddSharedServices(this IServiceCollection services)
    {
        services.AddSingleton(typeof(ILoggerPort<>), typeof(SerilogLoggerAdapter<>));
        services.AddScoped<ValidateRequestFilter>();

        return services;
    }
}