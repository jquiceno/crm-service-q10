using Microsoft.EntityFrameworkCore;
using Shared.Application.Ports;
using Shared.Domain.Aggregates;
using Shared.Domain.Entities;
using Shared.Domain.Errors;
using Shared.Domain.Interfaces;
using Shared.Domain.Pagination;
using Shared.Domain.Result;

namespace Infrastructure.Persistence.EntityFramework.Common;

public abstract class BaseAggregateRepository<TAggregate, TEntity, TId>(ApplicationDbContext context, ILoggerPort<object> logger)
    : IRepositoryBase<TAggregate, TId>
    where TAggregate : AggregateRoot<TEntity, TId>
    where TEntity : Entity<TId>
    where TId : notnull
{
    protected readonly DbSet<TEntity> DbSet = context.Set<TEntity>();

    protected abstract TAggregate ToAggregate(TEntity entity);
    protected abstract TEntity ToEntity(TAggregate aggregate);

    public virtual async Task<Result<TAggregate>> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await DbSet.FindAsync(new object[] { id }, cancellationToken);
            return entity is null ? GetNotFoundError(id) : ToAggregate(entity);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error retrieving {AggregateType} with id {Id}", typeof(TAggregate).Name, id);
            return PersistenceErrors.Failure();
        }
    }

    protected virtual DomainError GetNotFoundError(TId id) =>
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
                    Entities = g
                        .OrderBy(x => x)
                        .Skip(page.Skip)
                        .Take(page.PageSize)
                        .ToList()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (result is null)
            {
                return PagedResult<TAggregate>.Success([], 0);
            }

            IReadOnlyList<TAggregate> aggregates = [.. result.Entities.Select(ToAggregate)];
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
            await DbSet.AddAsync(ToEntity(aggregate), cancellationToken);
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
            DbSet.Update(ToEntity(aggregate));
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error updating {AggregateType}", typeof(TAggregate).Name);
            return PersistenceErrors.Failure();
        }
    }

    public virtual Result Remove(TAggregate aggregate)
    {
        try
        {
            DbSet.Remove(ToEntity(aggregate));
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error removing {AggregateType}", typeof(TAggregate).Name);
            return PersistenceErrors.Failure();
        }
    }
}
