using Infrastructure.Extensions;
using Infrastructure.Settings;

namespace Api.DependencyInjection;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<PersistenceSettings>()
            .Bind(configuration.GetSection(PersistenceSettings.SectionName));

        var persistenceSettings = configuration
            .GetSection(PersistenceSettings.SectionName)
            .Get<PersistenceSettings>() ?? new PersistenceSettings();

        var healthChecks = services.AddHealthChecks();

        if (persistenceSettings.Enabled)
        {
            if (string.IsNullOrWhiteSpace(persistenceSettings.ConnectionString))
            {
                throw new InvalidOperationException(
                    "Critical Error: PERSISTENCE is enabled but ConnectionString is missing. "
                    + "Set 'Persistence:ConnectionString' in appsettings.json or "
                    + "'Persistence__ConnectionString' as an environment variable. "
                    + "Application startup aborted.");
            }

            healthChecks.AddSqlServer(persistenceSettings.ConnectionString, tags: ["ready"]);
            services.AddEfCoreSqlServer(persistenceSettings.ConnectionString);
        }
        else
        {
            services.AddEfCoreInMemory();
        }

        return services;
    }
}
