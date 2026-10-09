using System.Collections.Immutable;
using System.Globalization;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Catalog;
using LinerNotes.Infrastructure.RecommendationSources.LastFm.Models;

namespace LinerNotes.Infrastructure.RecommendationSources.LastFm;

public sealed record LastFmTagSnapshot(WeightedTagVector Vector, ImmutableArray<TagObservation> Observations,
    ResponseReference Response, string? MissingReason, ImmutableArray<CoverageGap> Gaps);

/// <summary>The same supplied artist-tag interpretation for hydration and local pilot replay.</summary>
public static class LastFmTagEvidence
{
    public const string NormalizationVersion = "max-observed-descriptive-count-v1";
    public static ImmutableArray<string> Stoplist { get; } =
        ["albums i own", "beautiful", "cool", "favorite", "favorites", "favourite", "fip",
         "loved", "my favorites", "seen live", "spotify", "under 2000 listeners"];

    public static LastFmTagSnapshot Materialize(LastFmResponse<LastFmTagItem> response)
    {
        var observations = response.Select((tag, index) =>
        {
            var count = LastFmEvidenceParser.Count(tag.RawCount ?? tag.Count?.ToString(CultureInfo.InvariantCulture));
            string? reason = string.IsNullOrWhiteSpace(tag.Name) ? "missing_name" :
                Stoplist.Contains(tag.Name.Trim(), StringComparer.OrdinalIgnoreCase) ? "stoplist" :
                !count.IsValid ? count.MissingReason : count.Value == 0 ? "zero_count" : null;
            return new TagObservation(tag.Name, count, tag.Url, response.Reference, reason, tag.ResponsePosition ?? index + 1);
        }).OrderBy(t => t.Name.Trim().ToLowerInvariant(), StringComparer.Ordinal)
            .ThenBy(t => t.Count.RawValue, StringComparer.Ordinal).ToImmutableArray();
        var valid = observations.Where(t => t.ExclusionReason is null).ToArray();
        double maximum = valid.Length == 0 ? 0 : valid.Max(t => t.Count.Value!.Value);
        var weights = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var tag in valid)
        {
            string key = tag.Name.Trim().ToLowerInvariant();
            weights[key] = Math.Max(weights.GetValueOrDefault(key), tag.Count.Value!.Value / maximum);
        }
        string? missing = valid.Length == 0 ? "no_valid_descriptive_tag_counts" : null;
        var gaps = response.Gaps;
        if (missing is not null)
            gaps = gaps.Add(new(response.Reference.RequestIdentity, response.Reference.Method, missing, response.Reference));
        return new(WeightedTagVector.FromDictionary(weights), observations, response.Reference, missing, gaps);
    }
}
