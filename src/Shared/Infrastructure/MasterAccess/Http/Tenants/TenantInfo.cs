namespace Infrastructure.MasterAccess.Http.Tenants;

/// <summary>
/// Tenant database configuration resolved from the external master-access endpoint. Carries the
/// (decrypted) connection string plus non-secret identifiers useful for logging/telemetry.
/// </summary>
public sealed record TenantInfo(string EntityCode, string DbName, string ConnectionString);
