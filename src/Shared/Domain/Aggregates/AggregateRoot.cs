using Shared.Domain.Entities;
using Shared.Domain.Interfaces;

namespace Shared.Domain.Aggregates;

public abstract class AggregateRoot<TId> : EntityRoot<TId>, IAggregateRoot
    where TId : notnull
{
    protected abstract void Created();
}
