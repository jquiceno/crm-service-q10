using Infrastructure.Caching;
using Infrastructure.Extensions;
using Infrastructure.MasterAccess.Extensions;
using Infrastructure.MasterAccess.Http.Tenants;
using Infrastructure.Settings;

namespace Api.DependencyInjection;

public static class InfrastructureServiceExtensions
{
    /// <summary>Name of the readiness check that probes the tenant-resolver.</summary>
    public const string TenantInfoHealthCheckName = "tenant-info";

    /// <summary>
    /// Registers persistence, health checks and the application cache. Multitenancy is mandatory: the
    /// database connection is resolved per request from the tenant-resolver, so every prerequisite is
    /// verified here and a missing one aborts the boot rather than degrading into a fake database.
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddContextValidators();

        if (!configuration.IsMultitenancyEnabled())
        {
            throw new InvalidOperationException(
                "Critical Error: multitenancy (TenantResolverService:Enabled) is off. This service has no "
                + "single-tenant mode and no in-memory fallback: without the tenant-resolver there is no "
                + "database to connect to, and booting anyway would look healthy while dropping every write. "
                + "Set 'TenantResolverService:Enabled=true' in appsettings.json (or 'TenantResolverService__Enabled' "
                + "as an environment variable) together with the resolver URL, the encryption key and the L2 cache. "
                + "Application startup aborted.");
        }

        var tenantSettings = configuration
            .GetSection(TenantResolverServiceSettings.SectionName)
            .Get<TenantResolverServiceSettings>() ?? new TenantResolverServiceSettings();

        if (!Uri.TryCreate(tenantSettings.BaseUrl, UriKind.Absolute, out _))
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
        // HealthUri is the single normalized source shared with the startup probe and the HttpClient
        // base address, so the three can never probe divergent URLs.
        services.AddHealthChecks().AddUrlGroup(
            tenantSettings.HealthUri,
            name: TenantInfoHealthCheckName,
            tags: ["ready"]);

        // The only persistence mode: SQL Server, connection resolved per request from the tenant.
        services.AddEfCoreSqlServerPerTenant();

        services.AddDistributedCache(configuration);

        return services;
    }-
}
