namespace Shared.Domain.Entities;

public abstract class EntityRoot
{
    public DateTime? CreatedAt { get; protected set; }
    public DateTime? UpdatedAt { get; private set; }
}

public abstract class EntityRoot<TId> : EntityRoot where TId : notnull
{
    public TId Id { get; set; } = default!;

    protected EntityRoot() { }

    public override bool Equals(object? obj) =>
        obj is EntityRoot<TId> entity && EqualityComparer<TId>.Default.Equals(Id, entity.Id);

    public override int GetHashCode() => Id?.GetHashCode() ?? 0;

    public static bool operator ==(EntityRoot<TId>? left, EntityRoot<TId>? right) => Equals(left, right);

    public static bool operator !=(EntityRoot<TId>? left, EntityRoot<TId>? right) => !Equals(left, right);
}