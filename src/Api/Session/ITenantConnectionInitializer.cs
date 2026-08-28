namespace Api.Session;

/// <summary>
/// Write-side abstraction the tenant middleware uses to record what it resolved for the current
/// request: the connection string the per-tenant <c>DbContext</c> reads through
/// <c>IDbConnectionProvider</c>, and the tenant code the cache keys read through
/// <c>ITenantCodeProvider</c>. Deliberately segregated from both read sides (ISP) so each consumer
/// depends only on the member it needs.
/// </summary>
public interface ITenantConnectionInitializer
{
    void Initialize(string connectionString, string entityCode);
}
