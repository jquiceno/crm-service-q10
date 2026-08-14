using Shared.Domain.Entities;
using Shared.Domain.Interfaces;

namespace Shared.Domain.Aggregates;

public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot
    where TId : notnull
{
    public DateTime? CreatedAt { get; protected set; }
    public DateTime? UpdatedAt { get; protected set; }

    protected void SetCreatedAt(DateTime dateTime) => CreatedAt = dateTime;
    protected void SetUpdatedAt(DateTime dateTime) => UpdatedAt = dateTime;

    protected abstract void Created();
}
