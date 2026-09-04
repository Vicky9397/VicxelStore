namespace Dpm.BuildingBlocks.Domain;

public abstract class ValueObject : IEquatable<ValueObject>
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public bool Equals(ValueObject? other) =>
        other is not null
        && GetType() == other.GetType()
        && GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());

    public override bool Equals(object? obj) => Equals(obj as ValueObject);

    public override int GetHashCode() =>
        GetEqualityComponents().Aggregate(0, (hash, component) =>
            HashCode.Combine(hash, component));

    public static bool operator ==(ValueObject? left, ValueObject? right) =>
        left?.Equals(right) ?? right is null;

    public static bool operator !=(ValueObject? left, ValueObject? right) => !(left == right);
}
