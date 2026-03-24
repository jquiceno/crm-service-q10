namespace Domain.Common;

public abstract class Entity : IEquatable<Entity>
{
    public Guid Id { get; private init; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; private set; }

    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty.", nameof(id));

        Id = id;
    }

    protected Entity() { }

    public void SetUpdatedAtUtc() => UpdatedAtUtc = DateTime.UtcNow;

    public bool Equals(Entity? other) => other is not null && Id == other.Id;

    public override bool Equals(object? obj) => obj is Entity entity && Equals(entity);

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(Entity? left, Entity? right) => Equals(left, right);

    public static bool operator !=(Entity? left, Entity? right) => !Equals(left, right);
}
