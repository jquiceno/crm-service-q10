using Shared.Domain.Tenants.Aggregates;
using Shared.Results;

namespace Shared.Application.UseCases.GetTenant;

public interface IGetTenantByCodeUseCase
{
    Task<Result<TenantAggregate>> ExecuteAsync(GetTenantByCodeQuery query, CancellationToken cancellationToken = default);
}
