using Microsoft.EntityFrameworkCore;
using Shared.Application.Ports;
using Shared.Domain.Aggregates;
using Shared.Domain.Interfaces;
using Shared.Domain.Pagination;
using Shared.Results;
using Shared.Results.Errors;

namespace Infrastructure.Persistence.EntityFramework.Common;

public abstract class RepositoryBaseEF<TAggregate, TId>(ApplicationDbContext context, ILoggerPort<object> logger)
    : IRootRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : notnull
{
    protected DbSet<TAggregate> DbSet { get; } = context.Set<TAggregate>();

    public virtual async Task<Result<TAggregate>> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        try
        {
            var aggregate = await DbSet.FindAsync(new object[] { id }, cancellationToken).ConfigureAwait(false);
            return aggregate is null ? GetNotFoundError(id) : Result<TAggregate>.Success(aggregate);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error retrieving {AggregateType} with id {Id}", typeof(TAggregate).Name, id);
            return PersistenceErrors.Failure();
        }
    }

    protected virtual NotFoundError GetNotFoundError(TId id) =>
        SharedErrors.NotFound(typeof(TAggregate).Name, id!);

    public virtual async Task<PagedResult<TAggregate>> GetAllAsync(
        PageQuery page,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await DbSet
                .AsNoTracking()
                .GroupBy(x => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Items = g
                        .OrderBy(x => x)
                        .Skip(page.Skip)
                        .Take(page.PageSize)
                        .ToList()
                })
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (result is null)
                return PagedResult<TAggregate>.Success([], 0);

            IReadOnlyList<TAggregate> aggregates = [.. result.Items];
            return PagedResult<TAggregate>.Success(aggregates, result.Total);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error retrieving all {AggregateType}", typeof(TAggregate).Name);
            return PersistenceErrors.Failure();
        }
    }

    public virtual async Task<Result> AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default)
    {
        try
        {
            await DbSet.AddAsync(aggregate, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error adding {AggregateType}", typeof(TAggregate).Name);
            return PersistenceErrors.Failure();
        }
    }

    public virtual Result Update(TAggregate aggregate)
    {
        try
        {
            DbSet.Update(aggregate);
            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error updating {AggregateType}", typeof(TAggregate).Name);
            return PersistenceErrors.Failure();
        }
    }

    public virtual Result Remove(TAggregate aggregate)
    {
        try
        {
            DbSet.Remove(aggregate);
            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error removing {AggregateType}", typeof(TAggregate).Name);
            return PersistenceErrors.Failure();
        }
    }
}
