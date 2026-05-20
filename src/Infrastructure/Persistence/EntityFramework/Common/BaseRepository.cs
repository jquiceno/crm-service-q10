using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Shared.Domain.Errors;
using Shared.Domain.Result;

namespace Infrastructure.Persistence.EntityFramework.Common;

public abstract class BaseRepository<T>(ApplicationDbContext context) where T : class
{
    protected readonly DbSet<T> DbSet = context.Set<T>();

    public virtual async Task<Result<T>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await DbSet.FindAsync([id], cancellationToken);
            return entity is null ? GetNotFoundError(id) : entity;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return PersistenceErrors.Failure();
        }
    }

    protected virtual DomainError GetNotFoundError(Guid id) =>
        SharedErrors.NotFound(typeof(T).Name, id);

    public virtual async Task<Result<IReadOnlyList<T>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<T> entities = await DbSet.ToListAsync(cancellationToken);
            return Result<IReadOnlyList<T>>.Success(entities);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return PersistenceErrors.Failure();
        }
    }

    public virtual async Task<Result<IReadOnlyList<T>>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<T> entities = await DbSet.Where(predicate).ToListAsync(cancellationToken);
            return Result<IReadOnlyList<T>>.Success(entities);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return PersistenceErrors.Failure();
        }
    }

    public virtual async Task<Result> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        try
        {
            await DbSet.AddAsync(entity, cancellationToken);
            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return PersistenceErrors.Failure();
        }
    }

    public virtual Result Update(T entity)
    {
        try
        {
            DbSet.Update(entity);
            return Result.Success();
        }
        catch (Exception)
        {
            return PersistenceErrors.Failure();
        }
    }

    public virtual Result Remove(T entity)
    {
        try
        {
            DbSet.Remove(entity);
            return Result.Success();
        }
        catch (Exception)
        {
            return PersistenceErrors.Failure();
        }
    }
}
