using Shared.Results;

namespace Infrastructure.MasterAccess.Http.Tenants;

/// <summary>
/// Resolves a tenant's database configuration (culture + connection string) from the external
/// master-access endpoint.
/// </summary>
public interface ITenantInfoClient
{
    Task<Result<TenantInfo>> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
}
