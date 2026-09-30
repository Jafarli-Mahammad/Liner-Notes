namespace LinerNotes.Domain.Catalog;

/// <summary>
/// A normalized musical descriptor tag (e.g., "atmospheric black metal", "shoegaze", "90s").
/// Identity is strictly determined by the normalized (lowercase, trimmed) tag name.
/// </summary>
public sealed class Tag : IEquatable<Tag>, IComparable<Tag>
{
    public string DisplayName { get; }
    public string NormalizedName { get; }

    private Tag(string displayName, string normalizedName)
    {
        DisplayName = displayName;
        NormalizedName = normalizedName;
    }

    public static Tag Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tag name cannot be empty or whitespace.", nameof(name));

        var trimmed = name.Trim();
        var normalized = trimmed.ToLowerInvariant();
        return new Tag(trimmed, normalized);
    }

    public bool Equals(Tag? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(NormalizedName, other.NormalizedName, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => Equals(obj as Tag);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(NormalizedName);

    public int CompareTo(Tag? other)
    {
        if (other is null) return 1;
        return string.Compare(NormalizedName, other.NormalizedName, StringComparison.Ordinal);
    }

    public override string ToString() => DisplayName;

    public static bool operator ==(Tag? left, Tag? right) => Equals(left, right);
    public static bool operator !=(Tag? left, Tag? right) => !Equals(left, right);
}
