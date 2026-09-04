using Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Api.DependencyInjection;

public static class OutputCacheExtensions
{
    public static IServiceCollection ConfigureCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration
            .GetSection(CacheSettings.SectionName)
            .Get<CacheSettings>() ?? new CacheSettings();

        services.AddOptions<CacheSettings>()
            .Bind(configuration.GetSection(CacheSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (!settings.Enabled)
        {
            Console.WriteLine("[Cache] Output caching is disabled.");
            return services;
        }

        if (!string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            var serviceName = configuration[$"{ServiceInfoSettings.SectionName}:Name"] ?? "app";

            services.AddStackExchangeRedisOutputCache(options =>
            {
                options.Configuration = settings.ConnectionString;
                options.InstanceName = $"{serviceName}:";
            });
            Console.WriteLine("[Cache] Output caching backend: Redis.");
        }
        else
        {
            Console.WriteLine("[Cache] Output caching backend: in-memory (no ConnectionString set).");
        }

        services.AddOutputCache(options =>
        {
            options.DefaultExpirationTimeSpan = TimeSpan.FromSeconds(settings.DefaultTtlSeconds);

            // excludeDefaultPolicy: the base policy contributes the vary-by rules but must NOT
            // enable caching on its own
            options.AddBasePolicy(policy => policy
                .SetVaryByHeader("X-Entity-Code", "Accept-Language")
                .SetVaryByQuery("EntityCode"),
                excludeDefaultPolicy: true);
            // An empty header array is what VaryByHeaderPolicy reads as "do not vary by headers"; an
            // empty policy body would leave the base policy's tenant and locale headers in the key,
            // so "Global" would not be global. Only for data that is identical across tenants.
            options.AddPolicy("Global", p => p.SetVaryByHeader([]));
        });

        return services;
    }

    public static WebApplication UseCacheMiddleware(this WebApplication app)
    {
        var settings = app.Services.GetRequiredService<IOptions<CacheSettings>>().Value;

        if (settings.Enabled)
            app.UseOutputCache();

        return app;
    }
}
