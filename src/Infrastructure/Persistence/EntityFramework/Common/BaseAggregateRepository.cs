using Microsoft.EntityFrameworkCore;
using Shared.Domain;

namespace Infrastructure.Persistence.EntityFramework.Common;

public abstract class BaseAggregateRepository<TAggregate, TEntity>(ApplicationDbContext context)
    where TAggregate : AggregateRoot<TEntity>
    where TEntity : Entity
{
    protected readonly DbSet<TEntity> DbSet = context.Set<TEntity>();

    protected abstract TAggregate ToAggregate(TEntity entity);
    protected abstract TEntity ToEntity(TAggregate aggregate);

    public virtual async Task<TAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbSet.FindAsync([id], cancellationToken);
        return entity is null ? null : ToAggregate(entity);
    }

    public virtual async Task<IReadOnlyList<TAggregate>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbSet.ToListAsync(cancellationToken);
        return entities.Select(ToAggregate).ToList().AsReadOnly();
    }

    public virtual async Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default) =>
        await DbSet.AddAsync(ToEntity(aggregate), cancellationToken);

    public virtual void Update(TAggregate aggregate) => DbSet.Update(ToEntity(aggregate));

    public virtual void Remove(TAggregate aggregate) => DbSet.Remove(ToEntity(aggregate));

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
