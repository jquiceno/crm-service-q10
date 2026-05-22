using System;
using Infrastructure.Extensions;
using Infrastructure.Settings;

namespace Api.DependencyInjection;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddContextValidators();

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
            // Console.WriteLine is intentional: Serilog is not yet configured during service registration.
            Console.WriteLine("[Persistence] Persistence is disabled. Using in-memory database.");

            services.AddEfCoreInMemory();
        }

        return services;
    }
}
