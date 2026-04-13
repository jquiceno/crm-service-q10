using Infrastructure.Cache;
using Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;
using Shared.Application.Interfaces;

namespace Infrastructure.Extensions;

public static class CacheExtensions
{
    public static IServiceCollection AddCacheServices(
        this IServiceCollection services,
        CacheSettings settings)
    {
        if (!settings.Enabled)
        {
            // Console.WriteLine is intentional: Serilog is not yet configured during service registration.
            Console.WriteLine("[Cache] Cache is disabled. Using NullCacheService.");
        }
        else if (string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            Console.WriteLine("[Cache] Cache is enabled but ConnectionString is empty. Using NullCacheService.");
        }
        else
        {
            Console.WriteLine("[Cache] Using NullCacheService.");
        }

        services.AddSingleton<ICacheService, NullCacheService>();
        return services;
    }
}
