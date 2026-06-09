using Shared.Domain.Tenants.Aggregates;
using Shared.Results;

namespace Shared.Domain.Tenants.Ports;

public interface ITenantRepository
{
    Task<Result<TenantAggregate>> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
}
