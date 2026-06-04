using Microsoft.EntityFrameworkCore;
using Shared.Application.Ports;
using Shared.Domain.Tenants.Aggregates;
using Shared.Domain.Tenants.Errors;
using Shared.Domain.Tenants.Ports;
using Shared.MasterAccess.Persistence.EntityFramework.Tenants.Mappers;
using Shared.Results;
using Shared.Results.Errors;

namespace Shared.MasterAccess.Persistence.EntityFramework.Tenants;

public sealed class TenantRepository(ApplicationDbContext context, ILoggerPort<TenantRepository> logger) : ITenantRepository
{
    public async Task<Result<TenantAggregate>> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        try
        {
            var document = await context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Code == code, cancellationToken)
                .ConfigureAwait(false);

            return document is null
                ? TenantErrors.NotFound(code)
                : TenantRepositoryMapper.ToDomain(document);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error retrieving Tenant with code {Code}", code);
            return new DomainError("A persistence error occurred.", ErrorType.Internal);
        }
    }
}
