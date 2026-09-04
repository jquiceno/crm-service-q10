using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Infrastructure.MasterAccess.Http.Tenants;

public sealed class TenantResolverServiceSettings
{
    public const string SectionName = "TenantResolverService";

    public bool Enabled { get; init; }

    /// <summary>Base URL of the tenant-info endpoint. A trailing '/' is added automatically if missing.</summary>
    [Required, Url]
    [SuppressMessage(
        "Design",
        "CA1056:URI-like properties should not be strings",
        Justification = "Bound from configuration as a string; converted to Uri when configuring the typed HttpClient.")]
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// <see cref="BaseUrl"/> normalized to an absolute URI with a guaranteed trailing slash. Single source
    /// of truth for every URL derived from the base — the typed HttpClient's <c>BaseAddress</c>, the
    /// readiness health check and the startup probe all build from this so they can never diverge. The
    /// trailing slash is what makes relative segments (the tenant code, <c>"health"</c>) append instead of
    /// replacing the last path segment. Only valid once <see cref="BaseUrl"/> has been validated as an
    /// absolute URL; throws otherwise.
    /// </summary>
    public Uri BaseUri => new(BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);

    /// <summary>The resolver's health endpoint, derived from <see cref="BaseUri"/>.</summary>
    public Uri HealthUri => new(BaseUri, "health");

    [Range(1, 300)]
    public int TimeoutSeconds { get; init; } = 15;

    [Range(1, 1440)]
    public int CacheTtlMinutes { get; init; } = 10;

    [Required]
    public string EncryptionKey { get; init; } = string.Empty;

    /// <summary>
    /// Optional environment discriminator sent to the resolver as <c>?clientEnv=</c>. The resolver uses it
    /// to decide which connection string to hand back, so a developer machine asks for <c>local</c> and gets
    /// a connection string it can actually reach. Empty (the default) sends no query parameter at all, which
    /// is what every deployed environment does — the resolver then answers with its own default.
    /// </summary>
    public string ClientEnv { get; init; } = string.Empty;
}
