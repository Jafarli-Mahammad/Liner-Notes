using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Enums;

namespace LinerNotes.Domain.Taste;

public static class TasteVectorMaterializer
{
    public static WeightedTagVector Build(IReadOnlyList<TasteSignal> positiveSignals,
        IReadOnlyDictionary<string, WeightedTagVector> seedVectors)
    {
        var taste = new SortedDictionary<string, double>(StringComparer.Ordinal);
        foreach (var signal in positiveSignals.OrderBy(s => s.TargetType).ThenBy(s => s.NormalizedTargetValue, StringComparer.Ordinal)
            .ThenBy(s => s.Id.ToString("D"), StringComparer.Ordinal))
        {
            if (!double.IsFinite(signal.Weight) || signal.Weight < 0) throw new ArgumentException("Invalid positive signal weight.");
            if (signal.TargetType == TasteTargetType.Tag) Add(signal.NormalizedTargetValue, signal.Weight);
            else if (signal.TargetType == TasteTargetType.Artist && seedVectors.TryGetValue(signal.NormalizedTargetValue, out var seed))
                foreach (var tag in seed.Weights.OrderBy(t => t.Key, StringComparer.Ordinal)) Add(tag.Key, tag.Value * signal.Weight);
        }
        return WeightedTagVector.FromDictionary(taste);

        void Add(string name, double weight)
        {
            double sum = taste.GetValueOrDefault(name) + weight;
            if (!double.IsFinite(sum) || sum < 0) throw new ArgumentException("Nonfinite taste materialization.");
            if (sum > 0) taste[name] = sum;
        }
    }
}
