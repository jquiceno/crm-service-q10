using BusinessStatus.Domain.Aggregates;
using BusinessStatus.Domain.Enums;
using BusinessStatus.Domain.Errors;
using BusinessStatus.Domain.Queries;
using BusinessStatus.Domain.Repositories;
using Infrastructure.Adapters.Persistence.SqlServer;
using Infrastructure.Persistence.EntityFramework.BusinessStatuses.Mappers;
using Infrastructure.Persistence.EntityFramework.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Shared.Application.Ports;
using Shared.Domain.Pagination;
using Shared.Results;
using Shared.Results.Errors;

namespace Infrastructure.Persistence.EntityFramework.BusinessStatuses;

public sealed class BusinessStatusRepository(
    ApplicationDbContext context,
    ILoggerPort<BusinessStatusRepository> logger) : IBusinessStatusRepository
{
    private const string Origin = nameof(BusinessStatusRepository);

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
            var pattern = $"%{filter.Name.Trim()}%";
            query = query.Where(x => EF.Functions.Like(x.Name!, pattern));
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
