namespace Platform.Domain.Common;

public abstract class Entity<TKey> : IEquatable<Entity<TKey>>
    where TKey : notnull
{
    public TKey Id { get; protected set; } = default!;

    public bool Equals(Entity<TKey>? other)
        => other is not null && EqualityComparer<TKey>.Default.Equals(Id, other.Id);

    public override bool Equals(object? obj) => obj is Entity<TKey> e && Equals(e);

    public override int GetHashCode() => EqualityComparer<TKey>.Default.GetHashCode(Id);
}
