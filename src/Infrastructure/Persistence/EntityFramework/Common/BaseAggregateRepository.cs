using Microsoft.EntityFrameworkCore;
using Shared.Domain.Aggregates;
using Shared.Domain.Entities;
using Shared.Domain.Errors;
using Shared.Domain.Result;

namespace Infrastructure.Persistence.EntityFramework.Common;

public abstract class BaseAggregateRepository<TAggregate, TEntity>(ApplicationDbContext context)
    where TAggregate : AggregateRoot<TEntity>
    where TEntity : Entity
{
    protected readonly DbSet<TEntity> DbSet = context.Set<TEntity>();

    protected abstract TAggregate ToAggregate(TEntity entity);
    protected abstract TEntity ToEntity(TAggregate aggregate);

    public virtual async Task<Result<TAggregate>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await DbSet.FindAsync([id], cancellationToken);
            return entity is null ? GetNotFoundError(id) : ToAggregate(entity);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return SharedErrors.PersistenceFailure(ex.Message);
        }
    }

    protected virtual DomainError GetNotFoundError(Guid id) => SharedErrors.NotFound(typeof(TAggregate).Name, id);

    public virtual async Task<Result<IReadOnlyList<TAggregate>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var entities = await DbSet.ToListAsync(cancellationToken);
            IReadOnlyList<TAggregate> aggregates = entities.Select(ToAggregate).ToList();
            return Result<IReadOnlyList<TAggregate>>.Success(aggregates);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return SharedErrors.PersistenceFailure(ex.Message);
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
            return SharedErrors.PersistenceFailure(ex.Message);
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
            return SharedErrors.PersistenceFailure(ex.Message);
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
            return SharedErrors.PersistenceFailure(ex.Message);
        }
    }

    public async Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            return DbErrorClassifier.Classify(ex);
        }
    }
}
