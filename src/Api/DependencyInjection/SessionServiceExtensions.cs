using Api.HostedServices;
using Api.Middleware;
using Api.Session;
using Infrastructure.MasterAccess.Extensions;
using Infrastructure.MasterAccess.Http.Tenants;
using Shared.Application.Ports;

namespace Api.DependencyInjection;

public static class SessionServiceExtensions
{
    /// <summary>
    /// Wires per-request tenant resolution when multitenancy is enabled: the tenant-info HTTP client
    /// (via <c>AddMasterAccess</c>) plus one scoped instance exposed through two segregated interfaces —
    /// <see cref="ITenantConnectionInitializer"/> (the middleware writes) and
    /// <see cref="IDbConnectionProvider"/> (the per-tenant <c>DbContext</c> reads). A no-op when disabled.
    /// </summary>
    public static IServiceCollection AddSessionServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (!IsMultitenancyEnabled(configuration))
            return services;

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
    /// Adds the tenant-resolution middleware to the pipeline when multitenancy is enabled; a no-op
    /// otherwise. Register it after <c>UseCors</c> (so it never blocks a CORS preflight) and before
    /// the output cache (whose keys vary by tenant).
    /// </summary>
    public static WebApplication UseTenantResolution(this WebApplication app)
    {
        if (IsMultitenancyEnabled(app.Configuration))
            app.UseMiddleware<TenantMiddleware>();

        return app;
    }

    private static bool IsMultitenancyEnabled(IConfiguration configuration) =>
        configuration
            .GetSection(TenantResolverServiceSettings.SectionName)
            .Get<TenantResolverServiceSettings>()?.Enabled ?? false;
}
