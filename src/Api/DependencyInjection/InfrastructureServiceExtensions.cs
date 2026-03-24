using Api.Settings;
using Infrastructure.Extensions;

namespace Api.DependencyInjection;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var enablePersistence = configuration.GetValue<bool>("ENABLE_PERSISTENCE");

        if (enablePersistence)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
           
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Critical Error: PERSISTENCE is enabled but ConnectionString is missing. Application startup aborted.");
            }

            services.AddHealthChecks().AddSqlServer(connectionString);
            services.AddPersistence(connectionString);
        }

        return services;
    }
}
