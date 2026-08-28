using BusinessStatus.Domain.Aggregates;
using BusinessStatus.Domain.Enums;
using BusinessStatus.Domain.Errors;
using BusinessStatus.Domain.Queries;
using BusinessStatus.Domain.Repositories;
using Infrastructure.Adapters.Persistence.SqlServer;
using Infrastructure.Persistence.EntityFramework.BusinessStatuses.Mappers;
using Infrastructure.Persistence.EntityFramework.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Shared.Application.Caching;
using Shared.Application.Ports;
using Shared.Domain.Pagination;
using Shared.Results;
using Shared.Results.Errors;

namespace Infrastructure.Persistence.EntityFramework.BusinessStatuses;

public sealed class BusinessStatusRepository(
    ApplicationDbContext context,
    ILoggerPort<BusinessStatusRepository> logger,
    ICacheStore cacheStore,
    ITenantCodeProvider tenantCodeProvider) : IBusinessStatusRepository
{
    private const string Origin = nameof(BusinessStatusRepository);
    private const string CacheContext = "businessstatus";
    private const string ListResource = "list";

    private static readonly TimeSpan ListCacheTtl = TimeSpan.FromMinutes(10);

    public async Task<Result<BusinessStatusAggregate>> GetByIdAsync(
        int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var row = await context.BusinessStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                .ConfigureAwait(false);

            if (row is null)
                return NotFound(id);

            return BusinessStatusRepositoryMapper.ToDomain(row);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error retrieving business status with id {Id}", id);
            return PersistenceErrors.Failure(Origin);
        }
    }

    public async Task<Result<bool>> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var exists = await context.BusinessStatuses
                .AsNoTracking()
                .AnyAsync(x => x.Id == id, cancellationToken)
                .ConfigureAwait(false);

            return Result<bool>.Success(exists);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error checking existence of business status with id {Id}", id);
            return PersistenceErrors.Failure(Origin);
        }
    }

    public async Task<PagedResult<BusinessStatusAggregate>> GetAsync(
        BusinessStatusFilter filter, PageQuery page, CancellationToken cancellationToken = default)
    {
        try
        {
            // Null when there is no tenant to partition by, in which case nothing is cached rather
            // than one key being shared between tenants. Built inside the try so that any surprise
            // building it is handled like the rest, never as an unhandled failure of the listing.
            var cacheKey = ListCacheKey(filter, page);

            if (cacheKey is not null)
            {
                var cached = await cacheStore
                    .GetAsync<BusinessStatusListSnapshot>(cacheKey, cancellationToken)
                    .ConfigureAwait(false);

                if (cached is not null)
                    return PagedResult<BusinessStatusAggregate>.Success(
                        [.. cached.Items.Select(ToAggregate)], cached.TotalCount);
            }

            var query = ApplyFilter(context.BusinessStatuses.AsNoTracking(), filter);

            var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

            var rows = await query
                .OrderBy(x => x.Percentage)
                .ThenBy(x => x.Id)
                .Skip(page.Skip)
                .Take(page.PageSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            IReadOnlyList<BusinessStatusAggregate> items =
                [.. rows.Select(BusinessStatusRepositoryMapper.ToDomain)];

            // Only successes are cached: the failure paths below never reach this line.
            if (cacheKey is not null)
                await cacheStore
                    .SetAsync(cacheKey, ToSnapshot(items, totalCount), ListCacheTtl, cancellationToken)
                    .ConfigureAwait(false);

            return PagedResult<BusinessStatusAggregate>.Success(items, totalCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error retrieving the business status catalogue");
            return PersistenceErrors.Failure(Origin);
        }
    }

    public Task<PagedResult<BusinessStatusAggregate>> GetAllAsync(
        PageQuery page, CancellationToken cancellationToken = default) =>
        GetAsync(new BusinessStatusFilter(Name: null, IsActive: null, BusinessStatusKind.All), page, cancellationToken);

    public async Task<Result<IReadOnlyList<BusinessStatusAggregate>>> GetActiveTerminalsAsync(
        TerminalKind kind, CancellationToken cancellationToken = default)
    {
        var percentage = kind == TerminalKind.Won
            ? BusinessStatusAggregate.MaxPercentage
            : BusinessStatusAggregate.MinPercentage;

        try
        {
            var rows = await context.BusinessStatuses
                .AsNoTracking()
                .Where(x => x.IsActive == true && x.Percentage == percentage)
                .OrderBy(x => x.Percentage)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            IReadOnlyList<BusinessStatusAggregate> aggregates =
                [.. rows.Select(BusinessStatusRepositoryMapper.ToDomain)];

            return Result<IReadOnlyList<BusinessStatusAggregate>>.Success(aggregates);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error retrieving the active {Kind} business statuses", kind);
            return PersistenceErrors.Failure(Origin);
        }
    }

    public async Task<Result<BusinessStatusAggregate>> CreateAsync(
        BusinessStatusAggregate aggregate, CancellationToken cancellationToken = default)
    {
        try
        {
            var row = BusinessStatusRepositoryMapper.ToDocument(aggregate);

            await context.BusinessStatuses.AddAsync(row, cancellationToken).ConfigureAwait(false);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // The IDENTITY is only populated after SaveChanges.
            aggregate.AssignId(row.Id);

            return aggregate;
        }
        catch (DbUpdateException ex)
        {
            logger.Error(ex, "Database error inserting a business status");
            return SqlServerErrorClassifier.Classify(ex, Origin);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error inserting a business status");
            return PersistenceErrors.Failure(Origin);
        }
    }

    public async Task<Result> AddAsync(
        BusinessStatusAggregate aggregate, CancellationToken cancellationToken = default)
    {
        try
        {
            var row = BusinessStatusRepositoryMapper.ToDocument(aggregate);

            await context.BusinessStatuses.AddAsync(row, cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error queueing a business status insert");
            return PersistenceErrors.Failure(Origin);
        }
    }

    public Result Update(BusinessStatusAggregate aggregate)
    {
        try
        {
            var row = BusinessStatusRepositoryMapper.ToDocument(aggregate);

            row.Id = aggregate.Id;

            context.BusinessStatuses.Update(row);

            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error updating business status with id {Id}", aggregate.Id);
            return PersistenceErrors.Failure(Origin);
        }
    }

    public async Task<Result> RemoveAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var row = await context.BusinessStatuses
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                .ConfigureAwait(false);

            if (row is null)
                return NotFound(id);

            context.BusinessStatuses.Remove(row);

            return Result.Success();
        }
        catch (SqlException ex)
        {
            logger.Error(ex, "Database error removing business status with id {Id}", id);
            return SqlServerErrorClassifier.Classify(ex, Origin);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error removing business status with id {Id}", id);
            return PersistenceErrors.Failure(Origin);
        }
    }

    /// <summary>
    /// The key of a page of the catalogue, or <c>null</c> when there is nothing to partition by.
    /// <c>CacheKey</c> refuses a segment that is empty or contains <c>':'</c>, and a tenant code it
    /// cannot represent is not a reason to fail the listing: the cache is skipped exactly as when no
    /// tenant is resolved, so the endpoint degrades instead of answering 500.
    /// </summary>
    private string? ListCacheKey(BusinessStatusFilter filter, PageQuery page)
    {
        var tenantCode = tenantCodeProvider.Current;

        if (string.IsNullOrWhiteSpace(tenantCode))
            return null;

        if (tenantCode.Contains(':', StringComparison.Ordinal))
        {
            logger.Warning(
                "Tenant code {TenantCode} cannot be a cache key segment; the catalogue is served without L2 cache.",
                tenantCode);

            return null;
        }

        return CacheKey.For(CacheContext).Tenant(tenantCode).Resource(ListResource, FilterDigest(filter, page));
    }

    /// <summary>
    /// Identifies a filter-and-page combination inside the key. It is a SHA-256 digest and not
    /// <c>GetHashCode</c> because that one is randomized per process: two replicas would never share
    /// an entry, and a restart would silently orphan every key. The name is length-prefixed so
    /// ("a|b", null) and ("a", "b") cannot collide into one entry.
    /// </summary>
    private static string FilterDigest(BusinessStatusFilter filter, PageQuery page)
    {
        var canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"{filter.Name?.Length ?? -1}|{filter.Name}|{filter.IsActive}|{(int)filter.Kind}|{page.PageIndex}|{page.PageSize}");

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));

        return Convert.ToHexString(digest.AsSpan(0, 8));
    }

    private static BusinessStatusListSnapshot ToSnapshot(
        IReadOnlyList<BusinessStatusAggregate> items, int totalCount) =>
        new([.. items.Select(x => new BusinessStatusSnapshotItem(
                x.Id, x.Name, x.Percentage, x.Color?.Value, x.IsActive))],
            totalCount);

    private static BusinessStatusAggregate ToAggregate(BusinessStatusSnapshotItem item) =>
        BusinessStatusAggregate.Reconstruct(item.Id, item.Name, item.Percentage, item.Color, item.IsActive);

    private static NotFoundError NotFound(int id) =>
        BusinessStatusErrors.NotFound(id) with
        {
            Context = BusinessStatusErrors.Context,
            Origin = Origin
        };

    private static IQueryable<Entities.BusinessStatus> ApplyFilter(
        IQueryable<Entities.BusinessStatus> query, BusinessStatusFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Name))
        {
            // A partial-match filter, not a pattern the caller writes: the LIKE metacharacters
            // '%', '_' and '[' are escaped so a name of "%" matches the literal per cent sign, not
            // every row. The backslash is escaped first (it is the escape character declared below),
            // otherwise it would double-escape the ones that follow.
            var escaped = filter.Name.Trim()
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_")
                .Replace("[", "\\[");
            var pattern = $"%{escaped}%";
            query = query.Where(x => EF.Functions.Like(x.Name!, pattern, "\\"));
        }

        if (filter.IsActive.HasValue)
        {
            var isActive = filter.IsActive.Value;
            query = query.Where(x => x.IsActive == isActive);
        }

        return filter.Kind switch
        {
            BusinessStatusKind.Intermediate => query.Where(x =>
                x.Percentage != BusinessStatusAggregate.MinPercentage &&
                x.Percentage != BusinessStatusAggregate.MaxPercentage),

            BusinessStatusKind.Terminal => query.Where(x =>
                x.Percentage == BusinessStatusAggregate.MinPercentage ||
                x.Percentage == BusinessStatusAggregate.MaxPercentage),

            _ => query
        };
    }
}
