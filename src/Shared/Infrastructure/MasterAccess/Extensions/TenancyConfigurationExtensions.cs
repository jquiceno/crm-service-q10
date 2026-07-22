using Infrastructure.MasterAccess.Http.Tenants;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.MasterAccess.Extensions;

public static class TenancyConfigurationExtensions
{
    /// <summary>
    /// Single source of truth for the "multitenancy enabled" flag. Evaluate once at startup and pass the
    /// result to the registration/pipeline hooks so they can never disagree (a divergence would surface as
    /// an opaque DI-resolution failure at runtime instead of a clear decision).
    /// </summary>
    public static bool IsMultitenancyEnabled(this IConfiguration configuration) =>
        configuration
            .GetSection(TenantResolverServiceSettings.SectionName)
            .Get<TenantResolverServiceSettings>()?.Enabled ?? false;
}
