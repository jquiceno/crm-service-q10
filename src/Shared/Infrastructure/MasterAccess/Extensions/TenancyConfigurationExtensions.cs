using Infrastructure.MasterAccess.Http.Tenants;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.MasterAccess.Extensions;

public static class TenancyConfigurationExtensions
{
    /// <summary>
    /// Language-agnostic environment variable holding the tenant-resolver base URL, shared by every
    /// platform service regardless of stack (see the /platform/{env}/shared secret).
    /// </summary>
    public const string TenantResolverUrlVariable = "TENANT_RESOLVER_SERVICE_URL";

    /// <summary>
    /// Language-agnostic environment variable holding the passphrase used to decrypt per-tenant
    /// connection strings. Must match the key the tenant-resolver service encrypts with.
    /// </summary>
    public const string EncryptionKeyVariable = "CONNSTRING_ENCRYPTION_KEY";

    /// <summary>
    /// Single source of truth for the "multitenancy enabled" flag. Evaluate once at startup and pass the
    /// result to the registration/pipeline hooks so they can never disagree (a divergence would surface as
    /// an opaque DI-resolution failure at runtime instead of a clear decision).
    /// </summary>
    public static bool IsMultitenancyEnabled(this IConfiguration configuration) =>
        configuration
            .GetSection(TenantResolverServiceSettings.SectionName)
            .Get<TenantResolverServiceSettings>()?.Enabled ?? false;

    /// <summary>
    /// Maps the platform's language-agnostic environment variables onto the .NET configuration keys the
    /// options binder expects. The /platform/{env}/shared secret publishes one canonical name per value
    /// (e.g. TENANT_RESOLVER_SERVICE_URL) so every service — regardless of language — consumes the same
    /// variable; this alias is the .NET-specific bridge. Call it before anything reads the
    /// TenantResolverService section. Explicit .NET-style keys keep working: the alias only applies when
    /// the agnostic variable is present.
    /// </summary>
    public static IConfiguration AddTenantResolverEnvironmentAliases(this IConfiguration configuration)
    {
        MapAlias(configuration, TenantResolverUrlVariable, $"{TenantResolverServiceSettings.SectionName}:BaseUrl");
        MapAlias(configuration, EncryptionKeyVariable, $"{TenantResolverServiceSettings.SectionName}:EncryptionKey");

        return configuration;
    }

    private static void MapAlias(IConfiguration configuration, string sourceKey, string targetKey)
    {
        var value = configuration[sourceKey];

        if (!string.IsNullOrWhiteSpace(value))
        {
            configuration[targetKey] = value;
        }
    }
}
