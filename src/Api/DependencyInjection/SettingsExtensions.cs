using Api.Settings;

namespace Api.DependencyInjection;

/// <summary>
/// Extension methods for registering API layer configuration settings.
/// </summary>
public static class SettingsExtensions
{
    /// <summary>
    /// Registers all strongly-typed settings from appsettings.json for the API layer.
    /// </summary>
    public static IServiceCollection AddApiSettings(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AppInfoSettings>()
            .Bind(configuration.GetSection("AppInfo"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // services.AddOptions<AuthSettings>()...
        // services.AddOptions<CacheSettings>()...

        return services;
    }
}