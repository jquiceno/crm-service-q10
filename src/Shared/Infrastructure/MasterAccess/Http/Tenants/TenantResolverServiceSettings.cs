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

    [Range(1, 300)]
    public int TimeoutSeconds { get; init; } = 15;

    [Range(1, 1440)]
    public int CacheTtlMinutes { get; init; } = 10;

    [Required]
    public string EncryptionKey { get; init; } = string.Empty;
}
