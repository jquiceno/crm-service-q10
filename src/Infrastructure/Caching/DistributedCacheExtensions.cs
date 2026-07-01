using Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Application.Ports;
using StackExchange.Redis;

namespace Infrastructure.Caching;

public static class DistributedCacheExtensions
{
    /// <summary>
    /// Registers the L2 cache. Uses <see cref="RedisCacheStore"/> when
    /// <c>Cache:L2Enabled</c> is true and a connection string is present; otherwise a
    /// <see cref="NoOpCacheStore"/> (graceful degradation at startup).
    /// </summary>
    public static IServiceCollection AddDistributedCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration
            .GetSection(CacheSettings.SectionName)
            .Get<CacheSettings>() ?? new CacheSettings();

        if (!settings.L2Enabled || string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            // Console.WriteLine is intentional: Serilog is not yet configured during service registration.
            Console.WriteLine("[Cache] L2 application cache is disabled (NoOp).");
            services.AddSingleton<ICacheStore, NoOpCacheStore>();
            return services;
        }

        var options = ConfigurationOptions.Parse(settings.ConnectionString);
        options.AbortOnConnectFail = false;

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(options));
        services.AddSingleton<ICacheStore, RedisCacheStore>();
        Console.WriteLine("[Cache] L2 application cache backend: Redis.");

        return services;
    }
}
