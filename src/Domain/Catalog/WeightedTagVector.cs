using System.Collections.ObjectModel;

namespace LinerNotes.Domain.Catalog;

/// <summary>
/// Immutable vector of normalized musical tags mapped to continuous non-negative weights.
/// Serves as the mathematical basis for cosine similarity matching across candidates and taste profiles.
/// </summary>
public sealed class WeightedTagVector : IEquatable<WeightedTagVector>
{
    public static readonly WeightedTagVector Empty = new(new Dictionary<string, double>());

    private readonly ReadOnlyDictionary<string, double> _weights;
    private readonly Lazy<double> _magnitude;

    public IReadOnlyDictionary<string, double> Weights => _weights;
    public int Count => _weights.Count;
    public bool IsEmpty => _weights.Count == 0;
    public double Magnitude => _magnitude.Value;

    public double this[string tagName]
    {
        get
        {
            if (string.IsNullOrWhiteSpace(tagName)) return 0.0;
            return _weights.TryGetValue(tagName.Trim().ToLowerInvariant(), out var weight) ? weight : 0.0;
        }
    }

    private WeightedTagVector(Dictionary<string, double> weights)
    {
        _weights = new ReadOnlyDictionary<string, double>(weights);
        _magnitude = new Lazy<double>(CalculateMagnitude);
    }

    public static WeightedTagVector FromDictionary(IReadOnlyDictionary<string, double> dictionary)
    {
        ArgumentNullException.ThrowIfNull(dictionary);

        var normalized = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (key, value) in dictionary)
        {
            if (string.IsNullOrWhiteSpace(key) || value <= 0) continue;
            var normKey = key.Trim().ToLowerInvariant();
            normalized[normKey] = normalized.TryGetValue(normKey, out var existing)
                ? Math.Max(existing, value)
                : value;
        }

        return new WeightedTagVector(normalized);
    }

    public static WeightedTagVector FromTags(IEnumerable<WeightedTag> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        var dict = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var weightedTag in tags)
        {
            if (weightedTag.Weight <= 0) continue;
            var key = weightedTag.Tag.NormalizedName;
            dict[key] = dict.TryGetValue(key, out var existing)
                ? Math.Max(existing, weightedTag.Weight)
                : weightedTag.Weight;
        }

        return new WeightedTagVector(dict);
    }

    public double DotProduct(WeightedTagVector other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (IsEmpty || other.IsEmpty) return 0.0;

        // Iterate over the smaller set for efficiency
        var (smaller, larger) = Count <= other.Count ? (_weights, other._weights) : (other._weights, _weights);

        double dot = 0.0;
        foreach (var (tag, weight) in smaller)
        {
            if (larger.TryGetValue(tag, out var otherWeight))
            {
                dot += weight * otherWeight;
            }
        }

        return dot;
    }

    public double CosineSimilarity(WeightedTagVector other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (IsEmpty || other.IsEmpty) return 0.0;

        var magA = Magnitude;
        var magB = other.Magnitude;

        if (magA <= 0.0 || magB <= 0.0) return 0.0;

        var dot = DotProduct(other);
        var similarity = dot / (magA * magB);

        // Clamp to [0.0, 1.0] to guard against floating-point rounding errors
        if (similarity < 0.0) return 0.0;
        if (similarity > 1.0) return 1.0;
        return similarity;
    }

    public IReadOnlyList<(string TagName, double LeftWeight, double RightWeight, double Contribution)> GetTopOverlappingTags(
        WeightedTagVector other,
        int limit = 5)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (IsEmpty || other.IsEmpty || limit <= 0)
            return Array.Empty<(string, double, double, double)>();

        var overlaps = new List<(string TagName, double LeftWeight, double RightWeight, double Contribution)>();

        foreach (var (tag, leftWeight) in _weights)
        {
            if (other._weights.TryGetValue(tag, out var rightWeight))
            {
                overlaps.Add((tag, leftWeight, rightWeight, leftWeight * rightWeight));
            }
        }

        return overlaps
            .OrderByDescending(o => o.Contribution)
            .Take(limit)
            .ToList();
    }

    public WeightedTagVector Normalized()
    {
        if (IsEmpty || Magnitude <= 0.0) return Empty;

        var mag = Magnitude;
        var normalized = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (tag, weight) in _weights)
        {
            normalized[tag] = weight / mag;
        }

        return new WeightedTagVector(normalized);
    }

    private double CalculateMagnitude()
    {
        if (IsEmpty) return 0.0;

        double sumOfSquares = 0.0;
        foreach (var weight in _weights.Values)
        {
            sumOfSquares += weight * weight;
        }

        return Math.Sqrt(sumOfSquares);
    }

    public bool Equals(WeightedTagVector? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Count != other.Count) return false;

        foreach (var (tag, weight) in _weights)
        {
            if (!other._weights.TryGetValue(tag, out var otherWeight) ||
                Math.Abs(weight - otherWeight) > 1e-9)
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as WeightedTagVector);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var (tag, weight) in _weights.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            hash.Add(tag, StringComparer.Ordinal);
            hash.Add(Math.Round(weight, 6));
        }
        return hash.ToHashCode();
    }
}
