using Infrastructure.Caching;
using Infrastructure.Extensions;
using Infrastructure.MasterAccess.Http.Tenants;

namespace Api.DependencyInjection;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration,
        bool multitenancyEnabled)
    {
        services.AddContextValidators();

        var tenantSettings = configuration
            .GetSection(TenantResolverServiceSettings.SectionName)
            .Get<TenantResolverServiceSettings>() ?? new TenantResolverServiceSettings();

        var healthChecks = services.AddHealthChecks();

        if (multitenancyEnabled)
        {
            // Multitenant: the database connection is resolved per request from the tenant-resolver.
            if (!Uri.TryCreate(tenantSettings.BaseUrl, UriKind.Absolute, out var baseUri))
            {
                throw new InvalidOperationException(
                    "Critical Error: multitenancy (TenantResolverService:Enabled) is on but TenantResolverService:BaseUrl "
                    + "is missing or not a valid absolute URL. Set 'TenantResolverService:BaseUrl' in appsettings.json "
                    + "or 'TenantResolverService__BaseUrl' as an environment variable. Application startup aborted.");
            }

            if (string.IsNullOrWhiteSpace(tenantSettings.EncryptionKey))
            {
                throw new InvalidOperationException(
                    "Critical Error: multitenancy (TenantResolverService:Enabled) is on but TenantResolverService:EncryptionKey "
                    + "is missing. Set 'TenantResolverService:EncryptionKey' in appsettings.json "
                    + "or 'TenantResolverService__EncryptionKey' as an environment variable. Application startup aborted.");
            }

            // Readiness gates traffic on the resolver's own health endpoint (must return 2xx). The
            // startup gate (TenantResolverStartupProbe, in AddSessionServices) is the harder check.
            healthChecks.AddUrlGroup(
                new Uri(baseUri, "health"),
                name: "tenant-info",
                tags: ["ready"]);

            services.AddEfCoreSqlServerPerTenant();
        }
        else
        {
            // Console.WriteLine is intentional: Serilog is not yet configured during service registration.
            Console.WriteLine("[Persistence] Multitenancy is disabled. Using in-memory database.");

            services.AddEfCoreInMemory();
        }

        services.AddDistributedCache(configuration);

        return services;
    }
}
