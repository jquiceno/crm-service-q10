using Infrastructure.Caching;
using Infrastructure.Extensions;
using Infrastructure.MasterAccess.Http.Tenants;
using Infrastructure.Settings;

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
                    + "is missing or not a valid absolute URL. Set the 'TENANT_RESOLVER_SERVICE_URL' environment variable "
                    + "(platform shared secret) or 'TenantResolverService:BaseUrl' in appsettings.json. "
                    + "Application startup aborted.");
            }

            if (string.IsNullOrWhiteSpace(tenantSettings.EncryptionKey))
            {
                throw new InvalidOperationException(
                    "Critical Error: multitenancy (TenantResolverService:Enabled) is on but TenantResolverService:EncryptionKey "
                    + "is missing. Set the 'CONNSTRING_ENCRYPTION_KEY' environment variable "
                    + "(platform shared secret) or 'TenantResolverService:EncryptionKey' in appsettings.json. "
                    + "Application startup aborted.");
            }

            // Multitenant resolution must be cache-backed: without L2 the resolver is called over HTTP on
            // every request.
            var cacheSettings = configuration
                .GetSection(CacheSettings.SectionName)
                .Get<CacheSettings>() ?? new CacheSettings();

            if (!cacheSettings.L2Enabled || string.IsNullOrWhiteSpace(cacheSettings.ConnectionString))
            {
                throw new InvalidOperationException(
                    "Critical Error: multitenancy (TenantResolverService:Enabled) is on but the L2 application cache "
                    + "is off (Cache:L2Enabled=false or Cache:ConnectionString is empty). Without it the tenant "
                    + "resolver is called over HTTP on every request. Set 'Cache:L2Enabled=true' and "
                    + "'Cache:ConnectionString' in appsettings.json (or 'Cache__L2Enabled' / 'Cache__ConnectionString' "
                    + "as environment variables). Application startup aborted.");
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
