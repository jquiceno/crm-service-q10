using Infrastructure.MasterAccess.Persistence.EntityFramework;
using Infrastructure.MasterAccess.Persistence.EntityFramework.Tenants.Entities;
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
    public async Task<Result<TenantAggregate>> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var key = CacheKey.For("masteraccess").Resource("tenant", code);

        // The flat, proxy-free Tenant entity (fetched AsNoTracking) is safe to cache directly.
        var cached = await cache.GetAsync<Tenant>(key, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
            return TenantRepositoryMapper.ToDomain(cached);

        try
        {
            var document = await context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Code == code, cancellationToken)
                .ConfigureAwait(false);

            if (document is null)
                return TenantErrors.NotFound(code);

            await cache.SetAsync(key, document, TimeSpan.FromMinutes(10), cancellationToken).ConfigureAwait(false);
            return TenantRepositoryMapper.ToDomain(document);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error retrieving Tenant with code {Code}", code);
            return new DomainError("A persistence error occurred.", ErrorType.Internal);
        }
    }
}
