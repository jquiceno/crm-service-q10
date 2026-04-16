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

        services.AddOutputCache(options =>
        {
            options.DefaultExpirationTimeSpan = TimeSpan.FromSeconds(settings.DefaultTtlSeconds);

            options.AddBasePolicy(policy => policy
                .SetVaryByHeader("X-Tenant-Id", "Accept-Language"));

            options.AddPolicy("Global", p => { });
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
