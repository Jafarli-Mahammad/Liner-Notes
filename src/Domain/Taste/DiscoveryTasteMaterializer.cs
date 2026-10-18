using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Enums;
namespace LinerNotes.Domain.Taste;

public sealed record EntityTasteEvidence(Guid SignalId, string EntityIdentity, string Scope,
    WeightedTagVector Vector, double Influence, string? MissingReason);

/// <summary>Count each canonical entity's descriptive tags once; independent entities still contribute.</summary>
public static class DiscoveryTasteMaterializer
{
    public const string Version = "canonical-entity-max-v1";
    public static WeightedTagVector Build(IReadOnlyList<TasteSignal> signals, IReadOnlyList<EntityTasteEvidence> evidence)
    {
        if (signals.Count > 200 || evidence.Count > 200) throw new ArgumentException("taste_evidence_limit");
        if (signals.Any(s => !double.IsFinite(s.Weight) || s.Weight < 0)) throw new ArgumentException("invalid_signal_weight");
        // Derived artist and tag signals can share recommendation provenance; only entities index tag evidence.
        var weights = signals.Where(s => s.TargetType is TasteTargetType.Artist or TasteTargetType.Album)
            .ToDictionary(s => s.Id, s => s.Weight);
        var entities = new SortedDictionary<string, SortedDictionary<string, double>>(StringComparer.Ordinal);
        foreach (var item in evidence)
        {
            if (!weights.TryGetValue(item.SignalId, out double weight) || string.IsNullOrWhiteSpace(item.EntityIdentity) ||
                item.Scope is not ("artist" or "release-group") || !double.IsFinite(item.Influence) || item.Influence is < 0 or > 1)
                throw new ArgumentException("invalid_entity_evidence");
            if (!entities.TryGetValue(item.EntityIdentity, out var tags)) entities[item.EntityIdentity] = tags = new(StringComparer.Ordinal);
            foreach (var tag in item.Vector.Weights)
                tags[tag.Key] = Math.Max(tags.GetValueOrDefault(tag.Key), tag.Value * weight * item.Influence);
        }
        foreach (var signal in signals.Where(s => s.TargetType == TasteTargetType.Tag))
        {
            string key = "manual-tag:" + signal.NormalizedTargetValue;
            if (!entities.TryGetValue(key, out var tags)) entities[key] = tags = new(StringComparer.Ordinal);
            tags[signal.NormalizedTargetValue] = Math.Max(tags.GetValueOrDefault(signal.NormalizedTargetValue), signal.Weight);
        }
        var result = new SortedDictionary<string, double>(StringComparer.Ordinal);
        foreach (var tags in entities.Values)
            foreach (var tag in tags) result[tag.Key] = result.GetValueOrDefault(tag.Key) + tag.Value;
        return WeightedTagVector.FromDictionary(result);
    }
}
