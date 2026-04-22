using Api.Filters;
using Infrastructure.Logging;
using Shared.Application.Interfaces;

namespace Api.DependencyInjection;

public static class SharedServiceExtensions
{
    public static IServiceCollection AddSharedServices(this IServiceCollection services)
    {
        services.AddSingleton(typeof(ILoggerService<>), typeof(SerilogLogger<>));
        services.AddScoped<ValidateRequestFilter>();

        return services;
    }
}