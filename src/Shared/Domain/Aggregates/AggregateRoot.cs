using Shared.Domain.Entities;
using Shared.Domain.Interfaces;

namespace Shared.Domain.Aggregates;

public abstract class AggregateRoot<TEntity, TId> : IAggregateRoot
    where TEntity : EntityRoot<TId>
    where TId : notnull
{
    protected TEntity Entity { get; }

    protected AggregateRoot(TEntity entity)
    {
        Entity = entity ?? throw new ArgumentNullException(nameof(entity));
    }

    public TId Id => Entity.Id;
}
