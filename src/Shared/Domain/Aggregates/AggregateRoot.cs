using Shared.Domain.Entities;
using Shared.Domain.Interfaces;

namespace Shared.Domain.Aggregates;

public abstract class AggregateRoot<TEntity> : IAggregateRoot
    where TEntity : Entity
{
    protected readonly TEntity _entity;

    protected AggregateRoot(TEntity entity)
    {
        _entity = entity ?? throw new ArgumentNullException(nameof(entity));
    }

    public Guid Id => _entity.Id;
}
