using Shared.Domain.Entities;
using Shared.Domain.Interfaces;

namespace Shared.Domain.Aggregates;

public abstract class AggregateRoot<TEntity, TId> : IAggregateRoot
    where TEntity : Entity<TId>
{
    protected readonly TEntity _entity;

    protected AggregateRoot(TEntity entity)
    {
        _entity = entity ?? throw new ArgumentNullException(nameof(entity));
    }

    public TId Id => _entity.Id;
}
