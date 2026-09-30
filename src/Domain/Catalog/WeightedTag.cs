namespace LinerNotes.Domain.Catalog;

/// <summary>
/// Association of a normalized tag with a continuous strength/weight.
/// </summary>
public sealed class WeightedTag : IEquatable<WeightedTag>
{
    public Tag Tag { get; }
    public double Weight { get; }

    public WeightedTag(Tag tag, double weight)
    {
        ArgumentNullException.ThrowIfNull(tag);
        if (weight < 0)
            throw new ArgumentOutOfRangeException(nameof(weight), "Tag weight cannot be negative.");

        Tag = tag;
        Weight = weight;
    }

    public static WeightedTag Create(string tagName, double weight) =>
        new(Tag.Create(tagName), weight);

    public bool Equals(WeightedTag? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Tag.Equals(other.Tag) && Math.Abs(Weight - other.Weight) < 1e-9;
    }

    public override bool Equals(object? obj) => Equals(obj as WeightedTag);

    public override int GetHashCode() => HashCode.Combine(Tag, Weight);

    public override string ToString() => $"{Tag}: {Weight:F2}";
}
