using Infrastructure.MasterAccess.Persistence.EntityFramework;
using Infrastructure.MasterAccess.Persistence.EntityFramework.Tenants.Mappers;
using Microsoft.EntityFrameworkCore;
using Shared.Application.Caching;
using Shared.Application.Ports;
using Shared.Domain.Tenants.Aggregates;
using Shared.Domain.Tenants.Errors;
using Shared.Domain.Tenants.Ports;
using Shared.Results;
using Shared.Results.Errors;

namespace Infrastructure.MasterAccess.Persistence.EntityFramework.Tenants;

public sealed class TenantRepository(
    MasterAccessDbContext context,
    ILoggerPort<TenantRepository> logger,
    ICacheStore cache) : ITenantRepository
{
    public Task<Result<TenantAggregate>> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        cache.GetOrSetAsync(
            CacheKey.For("masteraccess").Resource("tenant", code),
            TimeSpan.FromMinutes(10),
            () => QueryByCodeAsync(code, cancellationToken),
            cancellationToken);

    private async Task<Result<TenantAggregate>> QueryByCodeAsync(string code, CancellationToken cancellationToken)
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
