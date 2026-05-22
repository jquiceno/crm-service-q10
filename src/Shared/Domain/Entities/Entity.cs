namespace Shared.Domain.Entities;

public abstract class Entity
{
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; private set; }

    public void SetUpdatedAtUtc() => UpdatedAtUtc = DateTime.UtcNow;
}

public abstract class Entity<TId> : Entity where TId : notnull
{
    public TId Id { get; set; } = default!;

    protected Entity() { }

    public override bool Equals(object? obj) =>
        obj is Entity<TId> entity && EqualityComparer<TId>.Default.Equals(Id, entity.Id);

    public override int GetHashCode() => Id?.GetHashCode() ?? 0;

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) => Equals(left, right);

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !Equals(left, right);
}