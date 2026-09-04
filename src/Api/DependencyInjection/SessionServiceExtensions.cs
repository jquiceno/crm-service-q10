using Api.HostedServices;
using Api.Middleware;
using Api.Session;
using Infrastructure.MasterAccess.Extensions;
using Shared.Application.Ports;

namespace Api.DependencyInjection;

public static class SessionServiceExtensions
{
    /// <summary>
    /// Wires per-request tenant resolution: the tenant-info HTTP client (via <c>AddMasterAccess</c>) plus
    /// one scoped instance exposed through two segregated interfaces —
    /// <see cref="ITenantConnectionInitializer"/> (the middleware writes) and
    /// <see cref="IDbConnectionProvider"/> (the per-tenant <c>DbContext</c> reads). Multitenancy is
    /// mandatory; <c>AddInfrastructureServices</c> has already aborted the boot if it is off.
    /// </summary>
    public static IServiceCollection AddSessionServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMasterAccess(configuration);

        // Hard startup gate: abort boot if the tenant-resolver endpoint is unreachable (runs in
        // StartingAsync, before Kestrel binds).
        services.AddHostedService<TenantResolverStartupProbe>();

        services.AddScoped<ITenantConnectionInitializer, TenantContext>();
        services.AddScoped<IDbConnectionProvider>(sp =>
            (IDbConnectionProvider)sp.GetRequiredService<ITenantConnectionInitializer>());

        return services;
    }

    /// <summary>
    /// Adds the tenant-resolution middleware to the pipeline. Register it after <c>UseCors</c> (so it
    /// never blocks a CORS preflight) and before the output cache (whose keys vary by tenant).
    /// </summary>
    public static WebApplication UseTenantResolution(this WebApplication app)
    {
        app.UseMiddleware<TenantMiddleware>();

        return app;
    }
}
