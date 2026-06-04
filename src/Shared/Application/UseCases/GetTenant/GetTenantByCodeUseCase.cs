using Shared.Domain.Tenants.Aggregates;
using Shared.Domain.Tenants.Errors;
using Shared.Domain.Tenants.Ports;
using Shared.Results;

namespace Shared.Application.UseCases.GetTenant;

public sealed record GetTenantByCodeQuery(string Code);

public sealed class GetTenantByCodeUseCase(ITenantRepository repository) : IGetTenantByCodeUseCase
{
    public async Task<Result<TenantAggregate>> ExecuteAsync(GetTenantByCodeQuery query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query.Code))
            return TenantErrors.CodeRequired;

        if (query.Code.Trim().Length > 20)
            return TenantErrors.CodeTooLong;

        return await repository.GetByCodeAsync(query.Code.Trim(), cancellationToken).ConfigureAwait(false);
    }
}
