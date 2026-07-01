using Infrastructure.MasterAccess.Persistence.EntityFramework;
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
        var cached = await cache.GetOrSetAsync(
            CacheKey.For("masteraccess").Resource("tenant", code),
            TimeSpan.FromMinutes(10),
            () => QueryByCodeAsync(code, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        return cached.IsSuccess
            ? Result<TenantAggregate>.Success(
                TenantAggregate.Reconstruct(cached.Value.Code, cached.Value.Database, cached.Value.ServerDatabase))
            : Result<TenantAggregate>.Failure(cached.Error);
    }

    private async Task<Result<TenantCacheModel>> QueryByCodeAsync(string code, CancellationToken cancellationToken)
    {
        try
        {
            var document = await context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Code == code, cancellationToken)
                .ConfigureAwait(false);

            return document is null
                ? TenantErrors.NotFound(code)
                : new TenantCacheModel(document.Code, document.Database, document.ServerDatabase);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error retrieving Tenant with code {Code}", code);
            return new DomainError("A persistence error occurred.", ErrorType.Internal);
        }
    }
}
