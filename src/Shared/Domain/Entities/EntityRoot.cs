namespace Shared.Domain.Entities;

public abstract class EntityRoot<TId> where TId : notnull
{
    public TId Id { get; protected set; } = default!;
    public DateTime? CreatedAt { get; protected set; }
    public DateTime? UpdatedAt { get; protected set; }

    protected EntityRoot() { }

    protected void SetCreatedAt(DateTime dateTime) => CreatedAt = dateTime;
    protected void SetUpdatedAt(DateTime dateTime) => UpdatedAt = dateTime;

    public override bool Equals(object? obj) =>
        obj is EntityRoot<TId> entity && EqualityComparer<TId>.Default.Equals(Id, entity.Id);

    public override int GetHashCode() => Id?.GetHashCode() ?? 0;

    public static bool operator ==(EntityRoot<TId>? left, EntityRoot<TId>? right) => Equals(left, right);

    public static bool operator !=(EntityRoot<TId>? left, EntityRoot<TId>? right) => !Equals(left, right);
}