namespace Api.Session;

/// <summary>
/// Write-side abstraction the tenant middleware uses to record the resolved connection string for the
/// current request. Deliberately segregated from <c>IDbConnectionProvider</c> (the read side) so each
/// consumer depends only on the member it needs (ISP): the middleware writes, the DbContext reads.
/// </summary>
public interface ITenantConnectionInitializer
{
    void Initialize(string connectionString);
}
